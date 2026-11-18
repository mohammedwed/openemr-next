using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>Maps an allergy to a FHIR R4 <c>AllergyIntolerance</c> (ADR 0079): active until it has an end date, then inactive; the substance coded from the diagnosis text; the reaction and its severity.</summary>
internal static class AllergyIntoleranceMapper
{
    public const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-allergyintolerance";
    private const string ClinicalSystem = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical";
    private const string VerificationSystem = "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification";
    private static readonly HashSet<string> VerificationCodes = new(StringComparer.OrdinalIgnoreCase) { "unconfirmed", "confirmed", "refuted", "entered-in-error" };

    public static AllergyIntolerance ToFhir(AllergyView view)
    {
        var resource = new AllergyIntolerance
        {
            Id = view.Uuid.ToString(),
            Meta = new Meta { Profile = [UsCoreProfile] },
            ClinicalStatus = new CodeableConcept(ClinicalSystem, view.EndDate is null ? "active" : "inactive"),
            Code = DiagnosisCodings.Concept(view.Diagnosis, view.Title),
            Patient = new ResourceReference { Reference = $"Patient/{view.PatientUuid}" }
        };

        if (view.LastModified is { } stamp)
        {
            resource.Meta.LastUpdated = FhirTime.Instant(stamp);
            resource.Meta.VersionId = ConcurrencyToken.From(stamp).ToETag().Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(view.Verification) && VerificationCodes.Contains(view.Verification.Trim()))
            resource.VerificationStatus = new CodeableConcept(VerificationSystem, view.Verification.Trim().ToLowerInvariant());

        if (view.BeginDate is { } begin)
            resource.Onset = new FhirDateTime(begin.Year, begin.Month, begin.Day);

        if (view.RecordedAt is { } recorded)
            resource.RecordedDateElement = new FhirDateTime(recorded.Year, recorded.Month, recorded.Day);

        var severity = Severity(view.Severity);
        if (!string.IsNullOrWhiteSpace(view.Reaction) || severity is not null)
        {
            var reaction = new AllergyIntolerance.ReactionComponent { Severity = severity };
            reaction.Manifestation = [new CodeableConcept { Text = string.IsNullOrWhiteSpace(view.Reaction) ? "Reaction not recorded" : view.Reaction.Trim() }];
            resource.Reaction = [reaction];
        }

        if (IsCritical(view.Severity))
            resource.Criticality = AllergyIntolerance.AllergyIntoleranceCriticality.High;

        if (!string.IsNullOrWhiteSpace(view.Comments))
            resource.Note = [new Annotation { Text = new Markdown(view.Comments.Trim()) }];

        return resource;
    }

    /// <summary>Legacy's severity options, onto FHIR's three. Anything it does not recognise is left out, not guessed.</summary>
    private static AllergyIntolerance.AllergyIntoleranceSeverity? Severity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "mild" or "mild_to_moderate" => AllergyIntolerance.AllergyIntoleranceSeverity.Mild,
        "moderate" => AllergyIntolerance.AllergyIntoleranceSeverity.Moderate,
        "severe" or "moderate_to_severe" or "life_threatening_severity" or "fatal" => AllergyIntolerance.AllergyIntoleranceSeverity.Severe,
        _ => null
    };

    private static bool IsCritical(string? severity) => severity?.Trim().ToLowerInvariant() is "life_threatening_severity" or "fatal";
}
