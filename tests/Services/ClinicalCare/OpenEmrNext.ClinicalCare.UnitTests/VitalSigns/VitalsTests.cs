using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

public class VitalsTests
{
    private static VitalsRecording Recording() => VitalsRecording.Create(
        EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 42),
        new VitalsInput(Weight: new Quantity(70m, MeasurementUnit.Kilogram), Pulse: 64), 7, new DateTime(2026, 11, 16, 9, 30, 0));

    [Fact]
    public void Record_takes_its_identity_from_the_allocator_not_from_itself()
    {
        var identity = VitalsIdentity.Create(Guid.NewGuid());

        var vitals = Vitals.Record(Recording(), identity);

        Assert.Equal(identity.Uuid, vitals.Uuid);
        Assert.Equal(0, vitals.Id);
        Assert.Equal(154.323584m, vitals.WeightPounds);
        Assert.Equal(64, vitals.Pulse);
    }

    [Fact]
    public void The_database_id_can_be_assigned_once_and_only_once()
    {
        var vitals = Vitals.Record(Recording(), VitalsIdentity.Create(Guid.NewGuid()));

        vitals.AssignGeneratedId(55);

        Assert.Equal(55, vitals.Id);
        Assert.Throws<InvalidOperationException>(() => vitals.AssignGeneratedId(56));
    }

    [Fact]
    public void An_identity_needs_a_uuid()
    {
        Assert.Throws<VitalsValidationException>(() => VitalsIdentity.Create(Guid.Empty));
    }
}
