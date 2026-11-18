using System.Globalization;
using System.Text.RegularExpressions;
using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>
/// Maps one vitals set to FHIR R4 vital-sign <c>Observation</c>s (ADR 0079 #3): one per measurement that was taken, and a blood-pressure panel when either pressure was. Legacy does the same split but keeps
/// each Observation's id in its own <c>uuid_mapping</c> table once it has served the set; here the id is <b>derived</b>, <c>{vitalsUuid}-{measurement}</c>, so it needs no table and no write, is the same every time, and can be
/// read back (a hash could not). It will not equal an id legacy has already issued for the same measurement, and a client must not mix the two sources.
/// </summary>
internal static partial class ObservationMapper
{
    public const string VitalSigns = "vital-signs";
    private const string Loinc = "http://loinc.org";
    private const string Ucum = "http://unitsofmeasure.org";
    private const string CategorySystem = "http://terminology.hl7.org/CodeSystem/observation-category";
    private const string UsCore = "http://hl7.org/fhir/us/core/StructureDefinition/";
    private const string BaseVitalSigns = "http://hl7.org/fhir/StructureDefinition/vitalsigns";

    /// <summary>The measurements, in the order a set's Observations are returned.</summary>
    public static readonly string[] Slugs =
        ["blood-pressure", "heart-rate", "respiratory-rate", "body-temperature", "oxygen-saturation", "body-weight", "body-height", "bmi", "waist-circumference", "head-circumference"];

    [GeneratedRegex("^(?<uuid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})-(?<slug>[a-z-]{1,40})$")]
    private static partial Regex IdPattern();

    public static string IdOf(Guid vitalsUuid, string slug) => $"{vitalsUuid}-{slug}";

    /// <returns><c>true</c> for an id of the form {uuid}-{measurement} that names a measurement this mapper knows.</returns>
    public static bool TryParseId(string id, out Guid vitalsUuid, out string slug)
    {
        vitalsUuid = Guid.Empty;
        slug = "";
        var match = IdPattern().Match(id);
        if (!match.Success || !Slugs.Contains(match.Groups["slug"].Value))
            return false;

        slug = match.Groups["slug"].Value;
        return Guid.TryParse(match.Groups["uuid"].Value, out vitalsUuid);
    }

    /// <summary>Every Observation the set gives, in <see cref="Slugs"/> order. A measurement that was not taken gives none.</summary>
    public static IReadOnlyList<Observation> ToFhir(PatientVitals set) =>
        Slugs.Select(slug => One(set, slug)).Where(o => o is not null).Select(o => o!).ToList();

    /// <returns>The one Observation, or <c>null</c> when the set has no such measurement.</returns>
    public static Observation? One(PatientVitals set, string slug)
    {
        var v = set.Vitals;
        return slug switch
        {
            "blood-pressure" when v.Systolic is not null || v.Diastolic is not null => Pressure(set),
            "heart-rate" when v.Pulse is { } p => Simple(set, slug, "8867-4", "Heart rate", "heart-rate", Q(p, "/min", "beats/minute")),
            "respiratory-rate" when v.Respiration is { } r => Simple(set, slug, "9279-1", "Respiratory rate", "respiratory-rate", Q(r, "/min", "breaths/minute")),
            "body-temperature" when v.TemperatureFahrenheit is { } t => Temperature(set, t),
            "oxygen-saturation" when v.OxygenSaturation is { } o => Oxygen(set, o),
            "body-weight" when v.WeightPounds is { } w => Simple(set, slug, "29463-7", "Body weight", "body-weight", Q(w, "[lb_av]", "lb")),
            "body-height" when v.HeightInches is { } h => Simple(set, slug, "8302-2", "Body height", "body-height", Q(h, "[in_i]", "in")),
            "bmi" when v.Bmi is { } b => Simple(set, slug, "39156-5", "Body mass index (BMI) [Ratio]", "bmi", Q(b, "kg/m2", "kg/m2")),
            "waist-circumference" when v.WaistInches is { } w => Simple(set, slug, "8280-0", "Waist Circumference at umbilicus by Tape measure", null, Q(w, "[in_i]", "in")),
            "head-circumference" when v.HeadInches is { } h => Simple(set, slug, "9843-4", "Head Occipital-frontal circumference", "head-circumference", Q(h, "[in_i]", "in")),
            _ => null
        };
    }

    private static Quantity Q(decimal value, string ucum, string unit) => new() { Value = value, Unit = unit, System = Ucum, Code = ucum };

    private static Observation Shell(PatientVitals set, string slug, string profile, CodeableConcept code)
    {
        var v = set.Vitals;
        var resource = new Observation
        {
            Id = IdOf(v.Uuid, slug),
            Meta = new Meta { Profile = [profile] },
            Status = ObservationStatus.Final,
            Category = [new CodeableConcept(CategorySystem, VitalSigns, "Vital Signs")],
            Code = code,
            Subject = new ResourceReference { Reference = $"Patient/{set.PatientUuid}" },
            Encounter = new ResourceReference { Reference = $"Encounter/{set.EncounterUuid}" }
        };

        if (v.RecordedAt is { } at)
            resource.Effective = FhirTime.DateTime(at);

        if (v.LastUpdated is { } stamp)
        {
            resource.Meta.LastUpdated = FhirTime.Instant(stamp);
            resource.Meta.VersionId = ConcurrencyToken.From(stamp).ToETag().Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(v.Note))
            resource.Note = [new Annotation { Text = new Markdown(v.Note.Trim()) }];

        return resource;
    }

    private static Observation Simple(PatientVitals set, string slug, string loinc, string display, string? usCoreProfile, Quantity value)
    {
        var resource = Shell(set, slug, usCoreProfile is null ? BaseVitalSigns : UsCore + "us-core-" + usCoreProfile, new CodeableConcept(Loinc, loinc, display));
        resource.Value = value;
        return resource;
    }

    private static Observation Temperature(PatientVitals set, decimal fahrenheit)
    {
        var resource = Simple(set, "body-temperature", "8310-5", "Body temperature", "body-temperature", Q(fahrenheit, "[degF]", "F"));
        if (!string.IsNullOrWhiteSpace(set.Vitals.TemperatureMethod))
            resource.Method = new CodeableConcept { Text = set.Vitals.TemperatureMethod.Trim() };
        return resource;
    }

    /// <summary>US Core's pulse-oximetry profile wants the pulse-oximetry LOINC code beside the general one.</summary>
    private static Observation Oxygen(PatientVitals set, decimal percent)
    {
        var resource = Simple(set, "oxygen-saturation", "2708-6", "Oxygen saturation in Arterial blood", "pulse-oximetry", Q(percent, "%", "%"));
        resource.Code.Coding.Add(new Coding(Loinc, "59408-5", "Oxygen saturation in Arterial blood by Pulse oximetry"));
        return resource;
    }

    /// <summary>The panel, with whichever pressure was taken as a component. A missing one is not invented.</summary>
    private static Observation Pressure(PatientVitals set)
    {
        var v = set.Vitals;
        var resource = Shell(set, "blood-pressure", UsCore + "us-core-blood-pressure", new CodeableConcept(Loinc, "85354-9", "Blood pressure panel with all children optional"));
        if (v.Systolic is { } systolic)
            resource.Component.Add(new Observation.ComponentComponent { Code = new CodeableConcept(Loinc, "8480-6", "Systolic blood pressure"), Value = Q(systolic, "mm[Hg]", "mmHg") });
        if (v.Diastolic is { } diastolic)
            resource.Component.Add(new Observation.ComponentComponent { Code = new CodeableConcept(Loinc, "8462-4", "Diastolic blood pressure"), Value = Q(diastolic, "mm[Hg]", "mmHg") });
        return resource;
    }
}
