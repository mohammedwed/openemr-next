using OpenEmrNext.PatientManagement.Domain.ReadModels;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>Everything one correction must commit together: the new values, the versions the caller may have read, and the audit row (ADR 0081).</summary>
/// <param name="OfferedVersions">The <c>ETag</c>s the caller sent in <c>If-Match</c>, compared with the version the adapter reads under a row lock (LEG-10).</param>
public sealed record DemographicsUpdateRequest(Guid Uuid, PatientDemographics Change, IReadOnlyList<string> OfferedVersions, DateTime NowUtc, AuditEntry Audit);

public enum DemographicsUpdateStatus { Updated, NotFound, Conflict }

/// <param name="CurrentVersion">The patient's current version when the result is <see cref="DemographicsUpdateStatus.Conflict"/>, so the caller can reload.</param>
public sealed record DemographicsUpdateOutcome(DemographicsUpdateStatus Status, string? CurrentVersion);

/// <summary>
/// Reads and corrects a patient's demographics (ADR 0081). A port so the coupling to <c>patient_data</c>, a 130-column table other writers use, stays in one adapter. The update touches only the columns this service
/// owns, only if the patient is still at the version the caller read, and commits the audit row in the same transaction.
/// </summary>
public interface IPatientDemographicsStore
{
    /// <returns>The patient's demographics and version, or <c>null</c> when no patient has this uuid.</returns>
    Task<PatientDemographicsView?> FindAsync(Guid uuid, CancellationToken ct);

    Task<DemographicsUpdateOutcome> UpdateAsync(DemographicsUpdateRequest request, CancellationToken ct);
}
