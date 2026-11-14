namespace OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

/// <summary>
/// The subset of <c>FhirPatientService::loadSearchParameters()</c>'s 16 search parameters this
/// service supports: every field below is combined with AND, as exact-or-contains matches -- legacy's
/// own search modifiers (<c>:missing</c>, <c>:exact</c>, date comparators) are not replicated, a
/// deliberate scope reduction for a first cut, not an oversight.
/// </summary>
public sealed record PatientFhirSearchCriteria(
    Guid? Id = null,
    string? Identifier = null,
    string? Name = null,
    string? Family = null,
    string? Given = null,
    DateOnly? BirthDate = null,
    string? Gender = null,
    string? Address = null,
    string? AddressCity = null,
    string? AddressPostalCode = null,
    string? AddressState = null,
    string? Email = null,
    string? Phone = null,
    string? Telecom = null,
    DateTime? LastUpdatedSince = null,
    Guid? GeneralPractitioner = null);
