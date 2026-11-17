using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Allergies;

/// <summary>The aggregate: identity handed in, the recorded facts kept, the database key assigned once (DOM-5, DOM-6, DOM-7).</summary>
public class AllergyTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly AllergyCodeData Codes = new(
        new HashSet<string> { "hives" }, new HashSet<string> { "severe" }, new HashSet<string> { "confirmed" });

    private static Allergy NewAllergy(Guid? uuid = null)
    {
        var recording = AllergyRecording.Create(
            PatientReference.Create(Guid.NewGuid(), 42), "Penicillin", null, null, null, "hives", "severe", "confirmed", "note", 7, Now, Codes);
        return Allergy.Record(recording, AllergyIdentity.Create(uuid ?? Guid.NewGuid()), Now);
    }

    [Fact]
    public void A_recorded_allergy_carries_the_identity_it_was_given_and_the_facts_recorded()
    {
        var uuid = Guid.NewGuid();

        var allergy = NewAllergy(uuid);

        Assert.Equal(uuid, allergy.Uuid);
        Assert.Equal("Penicillin", allergy.Title);
        Assert.Null(allergy.BeginDate);
        Assert.Equal("hives", allergy.Reaction);
        Assert.Equal("severe", allergy.Severity);
        Assert.Equal("confirmed", allergy.Verification);
        Assert.Equal(42, allergy.Patient.Pid);
    }

    [Fact]
    public void The_recorded_time_is_the_clocks_now() =>
        Assert.Equal(Now, NewAllergy().RecordedAt);

    [Fact]
    public void The_database_id_is_zero_until_assigned_and_can_be_assigned_once()
    {
        var allergy = NewAllergy();
        Assert.Equal(0, allergy.Id);

        allergy.AssignGeneratedId(55);

        Assert.Equal(55, allergy.Id);
        Assert.Throws<InvalidOperationException>(() => allergy.AssignGeneratedId(56));
    }

    [Fact]
    public void An_identity_needs_a_uuid() =>
        Assert.Throws<ClinicalCareValidationException>(() => AllergyIdentity.Create(Guid.Empty));
}
