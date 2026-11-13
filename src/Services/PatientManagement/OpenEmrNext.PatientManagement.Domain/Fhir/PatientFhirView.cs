namespace OpenEmrNext.PatientManagement.Domain.Fhir;

/// <summary>
/// Everything needed to render one patient as a FHIR Patient resource, already resolved -- every
/// code/system/display triple below (race, ethnicity, administrative sex, gender identity, tribal
/// affiliation, interpreter-needed, language) is pulled from <c>list_options</c> and defaulted to
/// legacy's own "Unknown"/data-absent-reason fallback by <c>EfPatientFhirRepository</c>, not here:
/// this is a flat read-model, not a place for further lookups. Mirrors the shape (not the exact fields)
/// of <see cref="OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.AdvanceDirectiveDocumentSummary"/>
/// and <see cref="OpenEmrNext.PatientManagement.Domain.Insurance.EffectiveInsurancePolicy"/> -- a
/// Domain-level output type for data this service reads back, not a write-side aggregate.
/// </summary>
/// <remarks>
/// Deliberately excludes legacy's previous-names history (<c>patient_history</c>) and
/// additional/historical addresses (<c>patient_additional_addresses</c>) -- only the current name and
/// current address (<c>patient_data</c>'s own columns) are mapped, satisfying US Core's "must support
/// one name, one address" bar without the historical-tracking complexity neither table this service
/// already owns needs.
/// </remarks>
public sealed record PatientFhirView(
    Guid Uuid,
    DateTime LastUpdated,
    string? Title,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? Suffix,
    string? Street,
    string? StreetLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? CountryCode,
    string? PhoneHome,
    string? PhoneBiz,
    string? PhoneCell,
    string? Email,
    string? EmailDirect,
    DateOnly? DateOfBirth,
    string Gender,
    string BirthSexCode,
    string RaceSystem,
    string RaceCode,
    string RaceDisplay,
    string? EthnicityOmbSystem,
    string? EthnicityOmbCode,
    string? EthnicityOmbDisplay,
    string EthnicityText,
    string? Ssn,
    string? PublicPatientId,
    bool LanguageKnown,
    string? LanguageCode,
    string? LanguageDisplay,
    Guid? GeneralPractitionerUuid,
    bool HasGenderIdentity,
    string GenderIdentitySystem,
    string GenderIdentityCode,
    string GenderIdentityDisplay,
    string SexIdentifiedSystem,
    string SexIdentifiedCode,
    string SexIdentifiedDisplay,
    string? TribalAffiliationSystem,
    string? TribalAffiliationCode,
    string? TribalAffiliationDisplay,
    string InterpreterNeededSystem,
    string InterpreterNeededCode,
    string InterpreterNeededDisplay,
    DateTime? DeceasedDateTime);
