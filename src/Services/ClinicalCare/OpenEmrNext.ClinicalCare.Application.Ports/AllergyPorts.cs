using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// What <c>RecordAllergyUseCase</c> needs from persistence: a new allergy, its <see cref="AllergyRecordedEvent"/> and its audit row committed together,
/// atomically (EVT-1, SEC-9), without the use case knowing how. "Together" includes the <c>uuid_registry</c> and <c>lists_touch</c> rows (LEG-2).
/// </summary>
public interface IAllergyRegistry
{
    /// <summary>Persists the allergy, its event and its audit row in one transaction and records the database-assigned id on the allergy. If it throws, nothing was written.</summary>
    Task RegisterAsync(Allergy allergy, AllergyRecordedEvent @event, AuditEntry audit, CancellationToken ct);
}

/// <summary>Hands a new allergy its identity (DOM-7). An allergy has only a uuid, so there is no shared counter to take a number from.</summary>
public interface IAllergyIdentityAllocator
{
    AllergyIdentity Allocate();
}

/// <summary>
/// The configured code lists an allergy's coded fields are checked against (legacy <c>list_options</c>: <c>reaction</c>, <c>severity_ccda</c>,
/// <c>allergyintolerance-verification</c>). A port so the coupling to legacy stays in one adapter (design set decision D6).
/// </summary>
public interface IAllergyCodeLists
{
    /// <returns>The valid codes of every list, with no <c>activity</c> filter. Empty sets (not an error) when none are configured.</returns>
    Task<AllergyCodeData> GetCodesAsync(CancellationToken ct);
}

/// <summary>Reads allergies without touching an aggregate (ADR 0054). Every statement is scoped to <c>type = 'allergy'</c> (ADR 0070 #2).</summary>
public interface IAllergyReader
{
    /// <returns>The allergy, or <c>null</c> when none has this uuid, or the row is of another type.</returns>
    Task<AllergyView?> FindAsync(Guid allergyUuid, CancellationToken ct);

    /// <returns>One bounded page of the patient's allergies, newest first by when they began, with the total.</returns>
    Task<AllergyPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct);
}
