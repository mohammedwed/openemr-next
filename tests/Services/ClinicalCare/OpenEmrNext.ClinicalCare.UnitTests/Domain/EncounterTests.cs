using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Domain;

public class EncounterTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 9, 30, 0);
    private static readonly EncounterReferenceData Reference =
        new([new EncounterClassOption("AMB", "Outpatient", true)], null);

    private static EncounterCreation Creation() =>
        EncounterCreation.Create(
            PatientReference.Create(Guid.NewGuid(), 42), "AMB", 5, null, "Cough", 0, null, 7, null, null, Now, Reference);

    [Fact]
    public void Create_takes_its_identity_from_the_allocator_not_from_itself()
    {
        var identity = EncounterIdentity.Create(Guid.NewGuid(), 1043);

        var encounter = Encounter.Create(Creation(), identity);

        Assert.Equal(identity.Uuid, encounter.Uuid);
        Assert.Equal(1043, encounter.Number);
        Assert.Equal(0, encounter.Id);
    }

    [Fact]
    public void Create_carries_the_validated_facts_through()
    {
        var creation = Creation();

        var encounter = Encounter.Create(creation, EncounterIdentity.Create(Guid.NewGuid(), 1));

        Assert.Equal(creation.Patient, encounter.Patient);
        Assert.Equal("AMB", encounter.ClassCode);
        Assert.Equal(Now, encounter.Date);
        Assert.Equal(7, encounter.ProviderId);
    }

    [Fact]
    public void The_database_id_can_be_assigned_once_and_only_once()
    {
        var encounter = Encounter.Create(Creation(), EncounterIdentity.Create(Guid.NewGuid(), 1));

        encounter.AssignGeneratedId(900);

        Assert.Equal(900, encounter.Id);
        Assert.Throws<InvalidOperationException>(() => encounter.AssignGeneratedId(901));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 1)]
    [InlineData("b1a9c5f0-0000-4000-8000-000000000001", 0)]
    public void An_identity_needs_a_uuid_and_a_positive_number(string uuid, long number)
    {
        Assert.Throws<EncounterValidationException>(() => EncounterIdentity.Create(Guid.Parse(uuid), number));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 1)]
    [InlineData("b1a9c5f0-0000-4000-8000-000000000001", 0)]
    public void A_patient_reference_needs_a_uuid_and_a_positive_pid(string uuid, long pid)
    {
        Assert.Throws<EncounterValidationException>(() => PatientReference.Create(Guid.Parse(uuid), pid));
    }
}
