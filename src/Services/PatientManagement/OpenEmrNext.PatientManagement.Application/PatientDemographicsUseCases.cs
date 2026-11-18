using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ReadModels;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>No patient has this uuid. Maps to 404.</summary>
public sealed class PatientNotFoundException(Guid uuid) : Exception($"No patient with uuid {uuid} exists.");

/// <summary>The patient changed after the caller read them. Carries the current <c>ETag</c> so the caller can reload. Maps to 412.</summary>
public sealed class PatientVersionConflictException(Guid uuid, string? currentVersion) : Exception($"Patient {uuid} changed after it was read.")
{
    public string? CurrentVersion { get; } = currentVersion;
}

/// <summary>One patient's editable demographics and the version they are at.</summary>
public sealed class GetPatientDemographicsUseCase(IPatientDemographicsStore store)
{
    public Task<PatientDemographicsView?> ExecuteAsync(Guid uuid, CancellationToken ct) => store.FindAsync(uuid, ct);
}

/// <param name="Changed">The names of the fields that changed; empty when the correction left the patient as they were (nothing was written, audited or re-scored).</param>
public sealed record DemographicsCorrectionResult(PatientDemographicsView Patient, IReadOnlyList<string> Changed);

/// <summary>
/// Corrects a patient's demographics (M5r, ADR 0081). Thin by design (ARC-4): find the patient, check the version the caller read, let the domain validate with registration's own rules, work out what changed, and hand the
/// conditional write to <see cref="IPatientDemographicsStore"/> to commit with its audit row. The order of refusals is the order every other write here uses: not found (404), changed since read (412), then the
/// request's own fields (400); each is audited as failed. A correction that changes nothing writes nothing.
/// </summary>
public sealed class UpdatePatientDemographicsUseCase(IPatientDemographicsStore store, IAuditLog auditLog)
{
    /// <exception cref="PatientNotFoundException">No patient has this uuid.</exception>
    /// <exception cref="PatientVersionConflictException">The patient is no longer the version the caller read. Nothing is written.</exception>
    /// <exception cref="PatientValidationException">A field fails registration's rules. Nothing is written.</exception>
    public async Task<DemographicsCorrectionResult> ExecuteAsync(
        Guid uuid, IReadOnlyList<string> offeredVersions, string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail,
        CancellationToken ct, AuditOrigin? origin = null)
    {
        try
        {
            return await CorrectAsync(uuid, offeredVersions, firstName, lastName, sex, dateOfBirth, email, trustedEmail, ct, origin);
        }
        catch (Exception ex) when (ex is PatientNotFoundException or PatientVersionConflictException or PatientValidationException)
        {
            // A refused correction changed nothing, so its audit row is its own statement.
            await auditLog.AppendAsync(Audit.Failure(origin, AuditActions.PatientUpdate, uuid.ToString()), ct);
            throw;
        }
    }

    private async Task<DemographicsCorrectionResult> CorrectAsync(
        Guid uuid, IReadOnlyList<string> offeredVersions, string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail,
        CancellationToken ct, AuditOrigin? origin)
    {
        var current = await store.FindAsync(uuid, ct) ?? throw new PatientNotFoundException(uuid);
        var currentVersion = current.Version().ToETag();

        // The precondition comes before the request is judged: an out-of-date caller is told so, whatever else is wrong with what they sent.
        if (!offeredVersions.Contains(currentVersion, StringComparer.Ordinal))
            throw new PatientVersionConflictException(uuid, currentVersion);

        var change = PatientDemographics.Create(firstName, lastName, sex, dateOfBirth, email, trustedEmail);
        var changed = current.ChangedBy(change);
        if (changed.Count == 0)
            return new DemographicsCorrectionResult(current, changed);

        var outcome = await store.UpdateAsync(
            new DemographicsUpdateRequest(uuid, change, offeredVersions, DateTime.UtcNow, Audit.Success(origin, AuditActions.PatientUpdate, uuid.ToString(), changed)), ct);

        switch (outcome.Status)
        {
            case DemographicsUpdateStatus.Updated:
                return new DemographicsCorrectionResult(await store.FindAsync(uuid, ct) ?? throw new PatientNotFoundException(uuid), changed);
            case DemographicsUpdateStatus.Conflict:
                throw new PatientVersionConflictException(uuid, outcome.CurrentVersion);
            default:
                throw new PatientNotFoundException(uuid);
        }
    }
}
