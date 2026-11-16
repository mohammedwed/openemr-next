using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Builds domain objects the same way production does: through the validating factories.</summary>
internal static class EncounterTestData
{
    public static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    public const long PatientPid = 42;
    public static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0);

    public static readonly EncounterReferenceData Reference = new(
        [new EncounterClassOption("AMB", "Outpatient", true), new EncounterClassOption("EMER", "Emergency Dept", false)],
        new FacilityOption(3, "Main Clinic", 11));

    public static EncounterCreation Creation(long providerId = 7, int facilityId = 3, string? reason = "Cough") =>
        EncounterCreation.Create(
            PatientReference.Create(PatientUuid, PatientPid), "AMB", 5, null, reason, facilityId, null, providerId,
            null, null, Now, Reference);

    public static Encounter NewEncounter(long number = 1001, Guid? uuid = null, long providerId = 7) =>
        Encounter.Create(Creation(providerId), EncounterIdentity.Create(uuid ?? Guid.CreateVersion7(), number));
}
