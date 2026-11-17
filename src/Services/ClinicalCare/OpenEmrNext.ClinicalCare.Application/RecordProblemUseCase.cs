using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Everything a caller supplies to record a problem. Raw facts: <c>ProblemRecording.Create</c> owns the rules.</summary>
public sealed record RecordProblemCommand(
    Guid PatientUuid,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    int Outcome,
    int Occurrence,
    string? ReferredBy,
    string? Comments,
    long ProviderId,
    RequestOrigin? Origin = null,
    string? Verification = null);

/// <summary>
/// Records a problem on a registered patient's list (M5c slice 1). Thin by design (ARC-4): resolve what the rules need through ports, let the domain
/// decide, hand the result to <see cref="IProblemRegistry"/> to commit with its event and audit row. A refused request is audited as failed (ADR 0069 #4).
/// </summary>
public sealed class RecordProblemUseCase(
    IPatientDirectory patients,
    IIssueCodeLists codeLists,
    IProblemIdentityAllocator identities,
    IProblemRegistry registry,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="PatientNotFoundException">No patient has the requested uuid. Nothing is persisted; a <c>failed</c> audit row is written.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is persisted; a <c>failed</c> audit row is written.</exception>
    public async Task<Problem> ExecuteAsync(RecordProblemCommand command, CancellationToken ct)
    {
        try
        {
            return await RecordAsync(command, ct);
        }
        catch (Exception ex) when (ex is PatientNotFoundException or ClinicalCareValidationException)
        {
            // No problem exists yet, so the row names the patient the caller asked about; if this write fails the request fails (fail closed).
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, Actors.Of(command.Origin, command.ProviderId),
                    AuditActions.ProblemRecord, command.PatientUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<Problem> RecordAsync(RecordProblemCommand command, CancellationToken ct)
    {
        var patient = await patients.FindAsync(command.PatientUuid, ct)
            ?? throw new PatientNotFoundException(command.PatientUuid);

        var codes = await codeLists.GetCodesAsync(ct);

        // Legacy datetime columns hold the server's local time, not UTC (LEG-11); the event and audit row carry UTC.
        var nowLocal = clock.GetLocalNow().DateTime;
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var recording = ProblemRecording.Create(
            patient, command.Title, command.BeginDate, command.EndDate, command.Diagnosis, command.Outcome, command.Occurrence,
            command.ReferredBy, command.Comments, command.ProviderId, nowLocal, codes, command.Verification);

        var problem = Problem.Record(recording, identities.Allocate(), nowLocal);

        await registry.RegisterAsync(
            problem,
            new ProblemRecordedEvent(problem.Uuid, patient.Uuid, patient.Pid, nowUtc),
            new AuditEntry(
                nowUtc, Actors.Of(command.Origin, command.ProviderId), AuditActions.ProblemRecord,
                problem.Uuid, AuditOutcome.Success, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
            ct);

        return problem;
    }
}
