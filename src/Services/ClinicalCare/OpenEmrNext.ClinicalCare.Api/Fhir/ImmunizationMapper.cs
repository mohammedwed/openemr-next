using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>
/// Maps a stored immunization to a FHIR R4 <c>Immunization</c> (ADR 0079), the way legacy's <c>FhirImmunizationService</c> does where it can: <c>entered-in-error</c> when the record was added in error, <c>completed</c>
/// for a completed one and <c>not-done</c> for any other status, with the refusal reason as <c>statusReason</c>; the CVX code as <c>vaccineCode</c>. Pure: it reads a view and the facts the endpoint looked up, and touches nothing.
/// </summary>
internal static class ImmunizationMapper
{
    public const string Cvx = "http://hl7.org/fhir/sid/cvx";
    public const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-immunization";
    private const string ActReason = "http://terminology.hl7.org/CodeSystem/v3-ActReason";
    private const string ProviderRole = "http://terminology.hl7.org/CodeSystem/v2-0443";
    private const string DataAbsent = "http://terminology.hl7.org/CodeSystem/data-absent-reason";

    /// <param name="practitioners">The OpenEMR uuid of each user the record names, so a person is a reference as well as a name (ADR 0079 #7). A user not in it is named by text only.</param>
    /// <param name="utcOffset">The offset the stored local times are in: the server's own, as the JSON API reads them (decision 9 of the pending-decisions note).</param>
    public static Immunization ToFhir(ImmunizationView view, IReadOnlyDictionary<long, Guid> practitioners, TimeSpan utcOffset)
    {
        var version = view.Version().ToETag().Trim('"');
        var resource = new Immunization
        {
            Id = view.Uuid.ToString(),
            Meta = new Meta { VersionId = version, Profile = [UsCoreProfile] },   // no lastUpdated: this table has no reliable stamp (ADR 0072, ADR 0079 #4)
            Status = FhirStatus(view),
            VaccineCode = VaccineCode(view),
            Patient = new ResourceReference { Reference = $"Patient/{view.PatientUuid}" },
            Occurrence = view.AdministeredDate is { } at ? new FhirDateTime(new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Unspecified), utcOffset)) : new FhirString("unknown")
        };

        if (view.EncounterUuid is { } encounter)
            resource.Encounter = new ResourceReference { Reference = $"Encounter/{encounter}" };

        if (view.StatusText() == "not-done" && !string.IsNullOrWhiteSpace(view.RefusalReason))
            resource.StatusReason = new CodeableConcept(ActReason, view.RefusalReason.Trim());

        if (!string.IsNullOrWhiteSpace(view.LotNumber))
            resource.LotNumber = view.LotNumber.Trim();

        if (view.ExpirationDate is { } expiry)
            resource.ExpirationDateElement = new Date(expiry.Year, expiry.Month, expiry.Day);

        if (!string.IsNullOrWhiteSpace(view.Manufacturer))
            resource.Manufacturer = new ResourceReference { Display = view.Manufacturer.Trim() };

        if (!string.IsNullOrWhiteSpace(view.AdministrationSite))
            resource.Site = new CodeableConcept { Text = view.AdministrationSite.Trim() };

        if (!string.IsNullOrWhiteSpace(view.Route))
            resource.Route = new CodeableConcept { Text = view.Route.Trim() };

        if (view.AmountAdministered is { } amount)
            resource.DoseQuantity = new Quantity { Value = amount, Unit = string.IsNullOrWhiteSpace(view.AmountUnit) ? null : view.AmountUnit.Trim() };

        if (!string.IsNullOrWhiteSpace(view.InformationSource))
            resource.ReportOrigin = new CodeableConcept { Text = view.InformationSource.Trim() };

        if (!string.IsNullOrWhiteSpace(view.ReasonCode) || !string.IsNullOrWhiteSpace(view.ReasonDescription))
            resource.ReasonCode = [new CodeableConcept { Text = string.IsNullOrWhiteSpace(view.ReasonDescription) ? view.ReasonCode!.Trim() : view.ReasonDescription.Trim() }];

        if (!string.IsNullOrWhiteSpace(view.Note))
            resource.Note = [new Annotation { Text = new Markdown(view.Note.Trim()) }];

        if (view.EducationDate is not null || view.VisDate is not null)
        {
            resource.Education =
            [
                new Immunization.EducationComponent
                {
                    // US Core requires a document type or a reference (imm-1). A VIS date is the publication date of the Vaccine Information Statement; an education date alone is patient education in general.
                    DocumentTypeElement = new FhirString(view.VisDate is not null ? "Vaccine Information Statement" : "Patient education"),
                    PublicationDateElement = view.VisDate is { } vis ? new FhirDateTime(vis.Year, vis.Month, vis.Day) : null,
                    PresentationDateElement = view.EducationDate is { } edu ? new FhirDateTime(edu.Year, edu.Month, edu.Day) : null
                }
            ];
        }

        AddPerformer(resource, "AP", "Administering Provider", view.AdministeredById, view.AdministeredBy, practitioners);
        AddPerformer(resource, "OP", "Ordering Provider", view.OrderingProvider, null, practitioners);
        return resource;
    }

    private static Immunization.ImmunizationStatusCodes FhirStatus(ImmunizationView view) => view.StatusText() switch
    {
        "entered-in-error" => Immunization.ImmunizationStatusCodes.EnteredInError,
        "completed" => Immunization.ImmunizationStatusCodes.Completed,
        _ => Immunization.ImmunizationStatusCodes.NotDone
    };

    /// <summary>The status as legacy decides it: entered in error beats everything, "Completed" is completed, anything else is not done.</summary>
    private static string StatusText(this ImmunizationView view) =>
        view.EnteredInError ? "entered-in-error"
        : string.Equals(view.CompletionStatus?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase) ? "completed"
        : "not-done";

    private static CodeableConcept VaccineCode(ImmunizationView view) =>
        string.IsNullOrWhiteSpace(view.CvxCode)
            ? new CodeableConcept { Coding = [new Coding(DataAbsent, "unknown")], Text = "Vaccine not recorded" }   // vaccineCode is required in FHIR; say so rather than invent one
            : new CodeableConcept(Cvx, view.CvxCode.Trim());

    private static void AddPerformer(Immunization resource, string role, string roleDisplay, long? userId, string? name, IReadOnlyDictionary<long, Guid> practitioners)
    {
        var hasReference = userId is { } id && practitioners.ContainsKey(id);
        var hasName = !string.IsNullOrWhiteSpace(name);
        if (!hasReference && !hasName)
            return;

        resource.Performer.Add(new Immunization.PerformerComponent
        {
            Function = new CodeableConcept(ProviderRole, role, roleDisplay),
            Actor = new ResourceReference
            {
                Reference = hasReference ? $"Practitioner/{practitioners[userId!.Value]}" : null,
                Display = hasName ? name!.Trim() : null
            }
        });
    }
}
