using Hl7.Fhir.Model;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>
/// Maps an encounter to a FHIR R4 <c>Encounter</c> (ADR 0079). Clinical Care does not track an encounter's progress, so every one is <c>finished</c>, which is what legacy's service says too. The class is the
/// stored ActEncounterCode; the type is the visit category's name; the provider is a practitioner reference with a name (they dangle, ADR 0079 #7).
/// </summary>
internal static class EncounterMapper
{
    public const string UsCoreProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-encounter";
    public const string NumberSystem = "urn:openemr:encounter-number";
    private const string ActCode = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string Participation = "http://terminology.hl7.org/CodeSystem/v3-ParticipationType";

    /// <param name="categories">Visit category names by id, for the type; an encounter whose category is not in it is typed "Encounter".</param>
    /// <param name="people">The provider's uuid and name, when the provider is a known user.</param>
    public static Encounter ToFhir(EncounterView view, IReadOnlyDictionary<int, string> categories, IReadOnlyDictionary<long, UserPerson> people)
    {
        var resource = new Encounter
        {
            Id = view.Uuid.ToString(),
            Meta = new Meta { Profile = [UsCoreProfile] },
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding(ActCode, view.ClassCode),
            Type = [new CodeableConcept { Text = view.CalendarCategoryId is { } id && categories.TryGetValue(id, out var name) ? name : "Encounter" }],
            Subject = new ResourceReference { Reference = $"Patient/{view.PatientUuid}" }
        };

        if (view.Number is { } number)
            resource.Identifier = [new Identifier(NumberSystem, number.ToString(System.Globalization.CultureInfo.InvariantCulture))];

        if (view.LastUpdate is { } stamp)
        {
            resource.Meta.LastUpdated = FhirTime.Instant(stamp);
            resource.Meta.VersionId = ConcurrencyToken.From(stamp).ToETag().Trim('"');
        }

        if (view.Date is { } start)
            resource.Period = new Period { StartElement = FhirTime.DateTime(start) };

        if (!string.IsNullOrWhiteSpace(view.Reason))
            resource.ReasonCode = [new CodeableConcept { Text = view.Reason.Trim() }];

        if (!string.IsNullOrWhiteSpace(view.FacilityName))
            resource.ServiceProvider = new ResourceReference { Display = view.FacilityName.Trim() };

        if (view.ProviderId > 0 && people.TryGetValue(view.ProviderId, out var person))
        {
            resource.Participant =
            [
                new Encounter.ParticipantComponent
                {
                    Type = [new CodeableConcept(Participation, "PPRF", "primary performer")],
                    Individual = new ResourceReference { Reference = $"Practitioner/{person.Uuid}", Display = person.DisplayName }
                }
            ];
        }

        return resource;
    }
}
