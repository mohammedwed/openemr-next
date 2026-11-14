using Hl7.Fhir.Model;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;
using OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// Assembles a real <see cref="Patient"/> (Firely's generated FHIR R4 model, not a hand-rolled shape)
/// from a resolved <see cref="PatientFhirView"/> -- ports <c>FhirPatientService::parseOpenEMRRecord()</c>'s
/// assembly order exactly, including which extensions get omitted entirely versus added with a
/// data-absent/"Unknown" fallback (the two are not interchangeable in legacy, and are kept that way
/// here), and which extensions carry a bare <c>valueCoding</c> versus a nested <c>valueCodeableConcept</c>
/// -- confirmed field by field against the real PHP source, not assumed from the other extensions'
/// shape.
/// </summary>
public static class FhirPatientMapper
{
    private const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient";
    private const string DataAbsentReasonSystem = "http://terminology.hl7.org/CodeSystem/data-absent-reason";

    public static Patient ToFhirPatient(PatientFhirView view)
    {
        var name = BuildName(view);

        var patient = new Patient
        {
            Id = view.Uuid.ToString(),
            Meta = new Meta
            {
                VersionId = "1",
                LastUpdated = view.LastUpdated,
                Profile = [UsCoreProfile]
            },
            Active = true,
            Gender = view.Gender switch
            {
                "male" => AdministrativeGender.Male,
                "female" => AdministrativeGender.Female,
                "unknown" => AdministrativeGender.Unknown,
                _ => AdministrativeGender.Other
            },
            BirthDate = view.DateOfBirth?.ToString("yyyy-MM-dd"),
            Deceased = view.DeceasedDateTime is { } d ? new FhirDateTime(d) : new FhirBoolean(false)
        };

        if (!string.IsNullOrEmpty(name.Family) || name.Given.Any())
        {
            patient.Text = new Narrative
            {
                Status = Narrative.NarrativeStatus.Generated,
                Div = $"""<div xmlns="http://www.w3.org/1999/xhtml"> <p>{string.Join(' ', name.Given.Append(name.Family ?? "").Where(s => !string.IsNullOrEmpty(s)))}</p></div>"""
            };
        }

        patient.Name.Add(name);
        patient.Address.AddRange(BuildAddress(view));
        patient.Telecom.AddRange(BuildTelecom(view));
        patient.Identifier.AddRange(BuildIdentifiers(view));
        patient.Communication.Add(BuildCommunication(view));
        if (view.GeneralPractitionerUuid is { } gp)
            patient.GeneralPractitioner.Add(new ResourceReference($"Practitioner/{gp}"));
        patient.Extension.AddRange(BuildExtensions(view));

        return patient;
    }

    private static HumanName BuildName(PatientFhirView view)
    {
        var name = new HumanName { Use = HumanName.NameUse.Official };
        if (!string.IsNullOrEmpty(view.FirstName)) name.Given = name.Given.Append(view.FirstName);
        if (!string.IsNullOrEmpty(view.MiddleName)) name.Given = name.Given.Append(view.MiddleName);
        if (!string.IsNullOrEmpty(view.LastName)) name.Family = view.LastName;
        if (!string.IsNullOrEmpty(view.Title)) name.Prefix = [view.Title];
        if (!string.IsNullOrEmpty(view.Suffix)) name.Suffix = [view.Suffix];
        return name;
    }

    private static List<Address> BuildAddress(PatientFhirView view)
    {
        if (string.IsNullOrEmpty(view.Street) && string.IsNullOrEmpty(view.City)
            && string.IsNullOrEmpty(view.State) && string.IsNullOrEmpty(view.PostalCode))
            return [];

        var line = new List<string>();
        if (!string.IsNullOrEmpty(view.Street)) line.Add(view.Street);
        if (!string.IsNullOrEmpty(view.StreetLine2)) line.Add(view.StreetLine2);

        return [new Address { Line = line, City = view.City, State = view.State, PostalCode = view.PostalCode, Country = view.CountryCode }];
    }

    private static List<ContactPoint> BuildTelecom(PatientFhirView view)
    {
        var telecom = new List<ContactPoint>();
        if (!string.IsNullOrEmpty(view.PhoneHome))
            telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Home, view.PhoneHome));
        if (!string.IsNullOrEmpty(view.PhoneBiz))
            telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Work, view.PhoneBiz));
        if (!string.IsNullOrEmpty(view.PhoneCell))
            telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Mobile, view.PhoneCell));
        if (!string.IsNullOrEmpty(view.Email))
            telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, ContactPoint.ContactPointUse.Home, view.Email));
        if (!string.IsNullOrEmpty(view.EmailDirect))
            telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, ContactPoint.ContactPointUse.Mobile, view.EmailDirect));
        return telecom;
    }

    private static List<Identifier> BuildIdentifiers(PatientFhirView view)
    {
        var identifiers = new List<Identifier>();
        if (!string.IsNullOrEmpty(view.Ssn))
            identifiers.Add(CreateIdentifier("SS", "http://hl7.org/fhir/sid/us-ssn", view.Ssn));
        if (!string.IsNullOrEmpty(view.PublicPatientId))
            identifiers.Add(CreateIdentifier("PT", "http://terminology.hl7.org/CodeSystem/v2-0203", view.PublicPatientId));
        return identifiers;
    }

    private static Identifier CreateIdentifier(string typeCode, string systemUri, string value) => new()
    {
        Use = Identifier.IdentifierUse.Official,
        Type = new CodeableConcept { Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v2-0203", typeCode)] },
        System = systemUri,
        Value = value
    };

    private static Patient.CommunicationComponent BuildCommunication(PatientFhirView view) => new()
    {
        Language = view.Language is { } language
            ? new CodeableConcept { Coding = [ToCoding(language)], Text = language.Display }
            : DataAbsentUnknown()
    };

    private static CodeableConcept DataAbsentUnknown() =>
        new() { Coding = [new Coding(DataAbsentReasonSystem, "unknown", "Unknown")], Text = "Unknown" };

    private static List<Extension> BuildExtensions(PatientFhirView view)
    {
        var extensions = new List<Extension>
        {
            new("http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex", new Code(view.BirthSexCode)),
            BuildRaceExtension(view),
            // Administrative sex (US Core 8.0.0): a bare valueCoding, same as interpreter-needed below --
            // confirmed against parseOpenEMRPatientSexExtension(), not assumed from gender identity's shape.
            new("http://hl7.org/fhir/us/core/StructureDefinition/us-core-sex", ToCoding(view.SexIdentified)),
            new("http://hl7.org/fhir/us/core/StructureDefinition/us-core-interpreter-needed", ToCoding(view.InterpreterNeeded))
        };

        var ethnicity = BuildEthnicityExtension(view);
        if (ethnicity is not null) extensions.Add(ethnicity);

        // Gender identity (US Core): a valueCodeableConcept wrapping the coding, NOT a bare valueCoding --
        // confirmed against parseOpenEMRGenderIdentity()'s setValueCodeableConcept() call. A null
        // GenderIdentity means the extension is omitted entirely (legacy's own empty-value guard).
        if (view.GenderIdentity is { } genderIdentity)
            extensions.Add(new Extension(
                "http://hl7.org/fhir/us/core/StructureDefinition/us-core-genderidentity",
                new CodeableConcept { Coding = [ToCoding(genderIdentity)] }));

        if (view.TribalAffiliation is { } tribalAffiliation)
        {
            var tribal = new Extension { Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-tribal-affiliation" };
            // Also a valueCodeableConcept, nested under its own child extension -- confirmed against
            // parseOpenEMRPatientTribalAffiliationExtension()'s setValueCodeableConcept() call.
            tribal.Extension.Add(new Extension(
                "tribalAffiliation", new CodeableConcept { Coding = [ToCoding(tribalAffiliation)] }));
            extensions.Add(tribal);
        }

        return extensions;
    }

    /// <summary>Always present: a "white"/"black"/etc. race always resolves to something, including
    /// "Unknown" when blank -- unlike <see cref="BuildEthnicityExtension"/>.</summary>
    private static Extension BuildRaceExtension(PatientFhirView view)
    {
        var race = new Extension { Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race" };
        race.Extension.Add(new Extension("ombCategory", ToCoding(view.Race)));
        race.Extension.Add(new Extension("text", new FhirString(view.Race.Display)));
        return race;
    }

    /// <summary>Omitted entirely when the patient's <c>ethnicity</c> column is blank or has no
    /// matching <c>list_options</c> row -- matches legacy's own <c>if (!empty($ethnicity))</c> guard,
    /// not a reduction from it.</summary>
    private static Extension? BuildEthnicityExtension(PatientFhirView view)
    {
        if (string.IsNullOrEmpty(view.EthnicityText))
            return null;

        var ethnicity = new Extension { Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity" };
        if (view.EthnicityOmb is { } ombCategory)
            ethnicity.Extension.Add(new Extension("ombCategory", ToCoding(ombCategory)));
        ethnicity.Extension.Add(new Extension("text", new FhirString(view.EthnicityText)));
        return ethnicity;
    }

    private static Coding ToCoding(ResolvedCode code) => new(code.System, code.Code, code.Display);
}
