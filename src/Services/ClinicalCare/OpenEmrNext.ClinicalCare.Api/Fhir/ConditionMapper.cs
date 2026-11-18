using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>Maps a problem to a FHIR R4 <c>Condition</c> in the problem list (ADR 0079): active until it has an end date or the resolved outcome, then resolved; coded from the diagnosis text.</summary>
internal static class ConditionMapper
{
    public const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-condition-problems-health-concerns";
    public const string ProblemListItem = "problem-list-item";
    private const string ClinicalSystem = "http://terminology.hl7.org/CodeSystem/condition-clinical";
    private const string VerificationSystem = "http://terminology.hl7.org/CodeSystem/condition-ver-status";
    private const string CategorySystem = "http://terminology.hl7.org/CodeSystem/condition-category";
    private static readonly HashSet<string> VerificationCodes = new(StringComparer.OrdinalIgnoreCase)
        { "unconfirmed", "provisional", "differential", "confirmed", "refuted", "entered-in-error" };

    public static Condition ToFhir(ProblemView view)
    {
        var resolved = view.EndDate is not null || view.Outcome == 1;
        var resource = new Condition
        {
            Id = view.Uuid.ToString(),
            Meta = new Meta { Profile = [UsCoreProfile] },
            ClinicalStatus = new CodeableConcept(ClinicalSystem, resolved ? "resolved" : "active"),
            Category = [new CodeableConcept(CategorySystem, ProblemListItem)],
            Code = DiagnosisCodings.Concept(view.Diagnosis, view.Title),
            Subject = new ResourceReference { Reference = $"Patient/{view.PatientUuid}" }
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

        if (view.EndDate is { } end)
            resource.Abatement = new FhirDateTime(end.Year, end.Month, end.Day);

        if (view.RecordedAt is { } recorded)
            resource.RecordedDateElement = new FhirDateTime(recorded.Year, recorded.Month, recorded.Day);

        if (!string.IsNullOrWhiteSpace(view.Comments))
            resource.Note = [new Annotation { Text = new Markdown(view.Comments.Trim()) }];

        return resource;
    }
}
