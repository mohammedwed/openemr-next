using OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

/// <summary>
/// Everything needed to render one patient as a FHIR Patient resource, already resolved -- every
/// <see cref="ResolvedCode"/> below (race, ethnicity, administrative sex, gender identity, tribal
/// affiliation, interpreter-needed, language) is pulled from <c>list_options</c> and defaulted to
/// legacy's own "Unknown"/data-absent-reason fallback by <c>EfPatientFhirRepository</c>, not here:
/// this is a flat read-model, not a place for further lookups. Mirrors the shape (not the exact fields)
/// of <see cref="OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels.AdvanceDirectiveDocumentSummary"/>
/// and <see cref="OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels.EffectiveInsurancePolicy"/> --
/// data this service reads back, not a write-side aggregate. Lives in the separate
/// <c>OpenEmrNext.PatientManagement.Domain.ReadModels</c> project, not <c>Domain</c> itself (ADR 0054):
/// the read side depends on <c>Domain</c>'s value objects (<see cref="ResolvedCode"/>), never the
/// reverse, so nothing here can ever be mistaken for part of an aggregate's own write-side invariants.
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
    ResolvedCode Race,
    ResolvedCode? EthnicityCategory,
    string EthnicityText,
    string? Ssn,
    string? PublicPatientId,
    ResolvedCode? Language,
    Guid? GeneralPractitionerUuid,
    ResolvedCode? GenderIdentity,
    ResolvedCode SexIdentified,
    ResolvedCode? TribalAffiliation,
    ResolvedCode InterpreterNeeded,
    DateTime? DeceasedDateTime);
