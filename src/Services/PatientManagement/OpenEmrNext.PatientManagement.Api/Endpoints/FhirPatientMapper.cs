using OpenEmrNext.PatientManagement.Api.Contracts.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// Assembles the wire-shaped <see cref="FhirPatient"/> from a resolved <see cref="PatientFhirView"/> --
/// ports <c>FhirPatientService::parseOpenEMRRecord()</c>'s assembly order exactly, including which
/// extensions get omitted entirely versus added with a data-absent/"Unknown" fallback (the two are not
/// interchangeable in legacy, and are kept that way here).
/// </summary>
public static class FhirPatientMapper
{
    private const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient";
    private const string DataAbsentReasonSystem = "http://terminology.hl7.org/CodeSystem/data-absent-reason";

    public static FhirPatient ToFhirPatient(PatientFhirView view)
    {
        var name = BuildName(view);
        var text = name.Given.Count > 0 || !string.IsNullOrEmpty(name.Family)
            ? new FhirText("generated", $"""<div xmlns="http://www.w3.org/1999/xhtml"> <p>{string.Join(' ', name.Given.Append(name.Family ?? "").Where(s => !string.IsNullOrEmpty(s)))}</p></div>""")
            : null;

        return new FhirPatient(
            ResourceType: "Patient",
            Id: view.Uuid.ToString(),
            Meta: new FhirMeta("1", ToInstant(view.LastUpdated), [UsCoreProfile]),
            Text: text,
            Active: true,
            Identifier: BuildIdentifiers(view),
            Name: [name],
            Telecom: BuildTelecom(view),
            Gender: view.Gender,
            BirthDate: view.DateOfBirth?.ToString("yyyy-MM-dd"),
            DeceasedBoolean: view.DeceasedDateTime is null ? false : null,
            DeceasedDateTime: view.DeceasedDateTime is { } d ? ToInstant(d) : null,
            Address: BuildAddress(view),
            Communication: [BuildCommunication(view)],
            GeneralPractitioner: view.GeneralPractitionerUuid is { } gp ? [new FhirReference($"Practitioner/{gp}")] : [],
            Extension: BuildExtensions(view));
    }

    private static FhirHumanName BuildName(PatientFhirView view)
    {
        var given = new List<string>();
        if (!string.IsNullOrEmpty(view.FirstName)) given.Add(view.FirstName);
        if (!string.IsNullOrEmpty(view.MiddleName)) given.Add(view.MiddleName);
        return new FhirHumanName(
            Use: "official",
            Family: string.IsNullOrEmpty(view.LastName) ? null : view.LastName,
            Given: given,
            Prefix: string.IsNullOrEmpty(view.Title) ? [] : [view.Title],
            Suffix: string.IsNullOrEmpty(view.Suffix) ? [] : [view.Suffix]);
    }

    private static List<FhirAddress> BuildAddress(PatientFhirView view)
    {
        if (string.IsNullOrEmpty(view.Street) && string.IsNullOrEmpty(view.City)
            && string.IsNullOrEmpty(view.State) && string.IsNullOrEmpty(view.PostalCode))
            return [];

        var line = new List<string>();
        if (!string.IsNullOrEmpty(view.Street)) line.Add(view.Street);
        if (!string.IsNullOrEmpty(view.StreetLine2)) line.Add(view.StreetLine2);

        return [new FhirAddress(line, view.City, view.State, view.PostalCode, view.CountryCode)];
    }

    private static List<FhirContactPoint> BuildTelecom(PatientFhirView view)
    {
        var telecom = new List<FhirContactPoint>();
        if (!string.IsNullOrEmpty(view.PhoneHome)) telecom.Add(new FhirContactPoint("phone", view.PhoneHome, "home"));
        if (!string.IsNullOrEmpty(view.PhoneBiz)) telecom.Add(new FhirContactPoint("phone", view.PhoneBiz, "work"));
        if (!string.IsNullOrEmpty(view.PhoneCell)) telecom.Add(new FhirContactPoint("phone", view.PhoneCell, "mobile"));
        if (!string.IsNullOrEmpty(view.Email)) telecom.Add(new FhirContactPoint("email", view.Email, "home"));
        if (!string.IsNullOrEmpty(view.EmailDirect)) telecom.Add(new FhirContactPoint("email", view.EmailDirect, "mobile"));
        return telecom;
    }

    private static List<FhirIdentifier> BuildIdentifiers(PatientFhirView view)
    {
        var identifiers = new List<FhirIdentifier>();
        if (!string.IsNullOrEmpty(view.Ssn))
            identifiers.Add(CreateIdentifier("SS", "http://hl7.org/fhir/sid/us-ssn", view.Ssn));
        if (!string.IsNullOrEmpty(view.PublicPatientId))
            identifiers.Add(CreateIdentifier("PT", "http://terminology.hl7.org/CodeSystem/v2-0203", view.PublicPatientId));
        return identifiers;
    }

    private static FhirIdentifier CreateIdentifier(string typeCode, string systemUri, string value) =>
        new("official",
            new FhirCodeableConcept([new FhirCoding("http://terminology.hl7.org/CodeSystem/v2-0203", typeCode, null)], null),
            systemUri, value);

    private static FhirPatientCommunication BuildCommunication(PatientFhirView view) =>
        new(view.LanguageKnown
            ? new FhirCodeableConcept([new FhirCoding("urn:ietf:bcp:47", view.LanguageCode, view.LanguageDisplay)], view.LanguageDisplay)
            : DataAbsentUnknown());

    private static FhirCodeableConcept DataAbsentUnknown() =>
        new([new FhirCoding(DataAbsentReasonSystem, "unknown", "Unknown")], "Unknown");

    private static List<FhirExtension> BuildExtensions(PatientFhirView view)
    {
        var extensions = new List<FhirExtension>
        {
            new("http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex", ValueCode: view.BirthSexCode),
            BuildRaceExtension(view),
            BuildSexExtension(view),
            BuildInterpreterNeededExtension(view)
        };

        var ethnicity = BuildEthnicityExtension(view);
        if (ethnicity is not null) extensions.Add(ethnicity);

        if (view.HasGenderIdentity)
            extensions.Add(BuildCodeableConceptExtension(
                "http://hl7.org/fhir/us/core/StructureDefinition/us-core-genderidentity",
                view.GenderIdentitySystem, view.GenderIdentityCode, view.GenderIdentityDisplay));

        if (view.TribalAffiliationSystem is not null)
            extensions.Add(new FhirExtension(
                "http://hl7.org/fhir/us/core/StructureDefinition/us-core-tribal-affiliation",
                Extension: [BuildCodeableConceptExtension(
                    "tribalAffiliation", view.TribalAffiliationSystem, view.TribalAffiliationCode!, view.TribalAffiliationDisplay!)]));

        return extensions;
    }

    /// <summary>Always present: a "white"/"black"/etc. race always resolves to something, including
    /// "Unknown" when blank -- unlike <see cref="BuildEthnicityExtension"/>.</summary>
    private static FhirExtension BuildRaceExtension(PatientFhirView view) => new(
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race",
        Extension:
        [
            BuildCodeableConceptExtension("ombCategory", view.RaceSystem, view.RaceCode, view.RaceDisplay),
            new FhirExtension("text", ValueString: view.RaceDisplay)
        ]);

    /// <summary>Omitted entirely when the patient's <c>ethnicity</c> column is blank or has no
    /// matching <c>list_options</c> row -- matches legacy's own <c>if (!empty($ethnicity))</c> guard,
    /// not a reduction from it.</summary>
    private static FhirExtension? BuildEthnicityExtension(PatientFhirView view)
    {
        if (string.IsNullOrEmpty(view.EthnicityText))
            return null;

        var children = new List<FhirExtension> { new("text", ValueString: view.EthnicityText) };
        if (view.EthnicityOmbSystem is not null)
        {
            children.Insert(0, BuildCodeableConceptExtension(
                "ombCategory", view.EthnicityOmbSystem, view.EthnicityOmbCode!, view.EthnicityOmbDisplay));
        }

        return new FhirExtension("http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity", Extension: children);
    }

    private static FhirExtension BuildSexExtension(PatientFhirView view) => BuildCodeableConceptExtension(
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-sex",
        view.SexIdentifiedSystem, view.SexIdentifiedCode, view.SexIdentifiedDisplay);

    private static FhirExtension BuildInterpreterNeededExtension(PatientFhirView view) => BuildCodeableConceptExtension(
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-interpreter-needed",
        view.InterpreterNeededSystem, view.InterpreterNeededCode, view.InterpreterNeededDisplay);

    private static FhirExtension BuildCodeableConceptExtension(string url, string system, string code, string? display) =>
        new(url, ValueCoding: new FhirCoding(system, code, display));

    private static string ToInstant(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
}
