using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Everything a caller supplies to record an allergy. Raw facts: <c>AllergyRecording.Create</c> owns the rules.</summary>
public sealed record RecordAllergyCommand(
    Guid PatientUuid,
    string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    string? Diagnosis,
    string? Reaction,
    string? Severity,
    string? Verification,
    string? Comments,
    long ProviderId,
    RequestOrigin? Origin = null);

/// <summary>
/// Records an allergy on a registered patient's list (M5c slice 2). Thin by design (ARC-4): resolve what the rules need through ports, let the domain
/// decide, hand the result to <see cref="IAllergyRegistry"/> to commit with its event and audit row. A refused request is audited as failed (ADR 0069 #4).
/// </summary>
public sealed class RecordAllergyUseCase(
    IPatientDirectory patients,
    IAllergyCodeLists codeLists,
    IAllergyIdentityAllocator identities,
    IAllergyRegistry registry,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="PatientNotFoundException">No patient has the requested uuid. Nothing is persisted; a <c>failed</c> audit row is written.</exception>
    /// <exception cref="ClinicalCareValidationException">One or more fields fail validation. Nothing is persisted; a <c>failed</c> audit row is written.</exception>
    public async Task<Allergy> ExecuteAsync(RecordAllergyCommand command, CancellationToken ct)
    {
        try
        {
            return await RecordAsync(command, ct);
        }
        catch (Exception ex) when (ex is PatientNotFoundException or ClinicalCareValidationException)
        {
            // No allergy exists yet, so the row names the patient the caller asked about; if this write fails the request fails (fail closed).
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, Actors.Of(command.Origin, command.ProviderId),
                    AuditActions.AllergyRecord, command.PatientUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<Allergy> RecordAsync(RecordAllergyCommand command, CancellationToken ct)
    {
        var patient = await patients.FindAsync(command.PatientUuid, ct)
            ?? throw new PatientNotFoundException(command.PatientUuid);

        var codes = await codeLists.GetCodesAsync(ct);

        // Legacy datetime columns hold the server's local time, not UTC (LEG-11); the event and audit row carry UTC.
        var nowLocal = clock.GetLocalNow().DateTime;
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var recording = AllergyRecording.Create(
            patient, command.Title, command.BeginDate, command.EndDate, command.Diagnosis, command.Reaction, command.Severity, command.Verification,
            command.Comments, command.ProviderId, nowLocal, codes);

        var allergy = Allergy.Record(recording, identities.Allocate(), nowLocal);

        await registry.RegisterAsync(
            allergy,
            new AllergyRecordedEvent(allergy.Uuid, patient.Uuid, patient.Pid, nowUtc),
            new AuditEntry(
                nowUtc, Actors.Of(command.Origin, command.ProviderId), AuditActions.AllergyRecord,
                allergy.Uuid, AuditOutcome.Success, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
            ct);

        return allergy;
    }
}

/// <summary>Reads one allergy by its uuid.</summary>
public sealed class GetAllergyUseCase(IAllergyReader reader)
{
    /// <returns>The allergy, or <c>null</c> when there is none (or it is of another issue type: ADR 0070 #2).</returns>
    public Task<AllergyView?> ExecuteAsync(Guid allergyUuid, CancellationToken ct) => reader.FindAsync(allergyUuid, ct);
}

/// <summary>Lists one patient's allergies, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListPatientAllergiesUseCase(IPatientDirectory patients, IAllergyReader reader)
{
    /// <exception cref="ClinicalCareValidationException">The page bounds are outside what is allowed.</exception>
    /// <exception cref="PatientNotFoundException">No patient has this uuid.</exception>
    public async Task<AllergyPage> ExecuteAsync(Guid patientUuid, int? limit, int? offset, CancellationToken ct)
    {
        // Bounds first: a caller asking for 10,000 rows is told so without a database round trip.
        var page = PageRequest.Create(limit, offset);

        var patient = await patients.FindAsync(patientUuid, ct)
            ?? throw new PatientNotFoundException(patientUuid);

        return await reader.ListForPatientAsync(patient, page, ct);
    }
}
