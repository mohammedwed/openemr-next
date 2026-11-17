using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The outbox payload for <c>ImmunizationRecorded</c>: identifiers only, no vaccine, lot or note (DOM-16, EVT-5).</summary>
public sealed record ImmunizationRecordedEvent(Guid ImmunizationUuid, Guid PatientUuid, long PatientPid, DateTime RecordedAtUtc)
{
    public const string EventType = "ImmunizationRecorded";
}

/// <summary>The outbox payload for <c>ImmunizationCorrected</c>: which fields changed, never their values.</summary>
public sealed record ImmunizationCorrectedEvent(
    Guid ImmunizationUuid, Guid? PatientUuid, long? PatientPid, IReadOnlyList<string> ChangedFields, DateTime CorrectedAtUtc)
{
    public const string EventType = "ImmunizationCorrected";
}

/// <summary>Hands a new immunization its identity (DOM-7).</summary>
public interface IImmunizationIdentityAllocator
{
    ImmunizationIdentity Allocate();
}

/// <summary>Persists a new immunization with its event and audit row in one transaction (EVT-1, SEC-9), recording the database-assigned id on it. If it throws, nothing was written.</summary>
public interface IImmunizationRegistry
{
    Task RegisterAsync(Immunization immunization, ImmunizationRecordedEvent @event, AuditEntry audit, CancellationToken ct);
}

/// <summary>Reads immunizations without touching an aggregate (ADR 0054).</summary>
public interface IImmunizationReader
{
    /// <returns>The immunization, or <c>null</c> when none has this uuid.</returns>
    Task<ImmunizationView?> FindAsync(Guid immunizationUuid, CancellationToken ct);

    /// <returns>One bounded page of the patient's immunizations, most recently administered first, with the total.</returns>
    Task<ImmunizationPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct);
}

/// <summary>The configured code lists an immunization's coded fields are checked against (one adapter holds the coupling to legacy, design decision D6).</summary>
public interface IImmunizationCodeLists
{
    Task<ImmunizationCodeData> GetCodesAsync(CancellationToken ct);
}

/// <param name="Expected">The version the caller read; the adapter verifies it under a row lock (ADR 0072).</param>
public sealed record ImmunizationUpdateRequest(
    Guid ImmunizationUuid, long PatientPid, ContentVersion Expected, ImmunizationFacts Facts, ImmunizationCorrectedEvent Event, AuditEntry Audit);

/// <param name="Version">For <see cref="UpdateStatus.Conflict"/>, the row's current version.</param>
public sealed record ImmunizationUpdateOutcome(UpdateStatus Status, ContentVersion? Version);

/// <summary>Replaces an immunization's editable set if it is still the version the caller read, committing the row, its event and its audit row together.</summary>
public interface IImmunizationUpdater
{
    Task<ImmunizationUpdateOutcome> UpdateAsync(ImmunizationUpdateRequest request, CancellationToken ct);
}
