namespace OpenEmrNext.PatientManagement.Api.Contracts.Fhir;

/// <summary>
/// A hand-rolled subset of the FHIR R4 Patient resource shape -- the same approach legacy's own PHP
/// FHIR classes take (no external FHIR library there either), covering only the fields
/// <see cref="FhirPatientMapper"/> actually populates. Property names are PascalCase; the default
/// Minimal API JSON options already camelCase on write, which happens to match every FHIR field name
/// used here exactly (<c>ResourceType</c> -&gt; <c>resourceType</c>, <c>BirthDate</c> -&gt;
/// <c>birthDate</c>, etc.), so no <c>JsonPropertyName</c> attributes are needed.
/// </summary>
/// <remarks>
/// Targets US Core 8.0.0 only -- legacy branches its extension shapes across three profile versions
/// (3.1.1/7.0.0/8.0.0); this service has no installed base to stay compatible with, so there is no
/// reason to replicate versions nothing here will ever serve. Strict FHIR null-omission (absent
/// fields, not JSON <c>null</c>) is not replicated byte-for-byte -- a pragmatic simplification, not a
/// correctness gap in the values themselves.
/// </remarks>
public sealed record FhirPatient(
    string ResourceType,
    string Id,
    FhirMeta Meta,
    FhirText? Text,
    bool Active,
    List<FhirIdentifier> Identifier,
    List<FhirHumanName> Name,
    List<FhirContactPoint> Telecom,
    string Gender,
    string? BirthDate,
    bool? DeceasedBoolean,
    string? DeceasedDateTime,
    List<FhirAddress> Address,
    List<FhirPatientCommunication> Communication,
    List<FhirReference> GeneralPractitioner,
    List<FhirExtension> Extension);

public sealed record FhirMeta(string VersionId, string LastUpdated, List<string> Profile);

public sealed record FhirText(string Status, string Div);

public sealed record FhirCoding(string? System, string? Code, string? Display);

public sealed record FhirCodeableConcept(List<FhirCoding>? Coding, string? Text);

/// <summary>Only one of the <c>Value*</c> fields is ever non-null on a given instance, matching FHIR's
/// own polymorphic <c>value[x]</c> convention; <see cref="Extension"/> carries nested child
/// extensions (race/ethnicity's ombCategory + text, for example).</summary>
public sealed record FhirExtension(
    string Url,
    string? ValueCode = null,
    FhirCoding? ValueCoding = null,
    FhirCodeableConcept? ValueCodeableConcept = null,
    string? ValueString = null,
    List<FhirExtension>? Extension = null);

public sealed record FhirIdentifier(string Use, FhirCodeableConcept Type, string System, string Value);

public sealed record FhirHumanName(string Use, string? Family, List<string> Given, List<string> Prefix, List<string> Suffix);

public sealed record FhirAddress(List<string> Line, string? City, string? State, string? PostalCode, string? Country);

public sealed record FhirContactPoint(string System, string Value, string Use);

public sealed record FhirReference(string Reference);

public sealed record FhirPatientCommunication(FhirCodeableConcept Language);

/// <summary>One entry in a FHIR search-set <see cref="FhirBundle"/>.</summary>
public sealed record FhirBundleEntry(string FullUrl, FhirPatient Resource, FhirBundleSearch Search);

public sealed record FhirBundleSearch(string Mode);

public sealed record FhirBundle(string ResourceType, string Type, int Total, List<FhirBundleEntry> Entry);
