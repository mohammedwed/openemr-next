using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Immunizations;

/// <summary>The immunization aggregate, its identity, and the content version of its read model (ADR 0072).</summary>
public class ImmunizationAggregateTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly ImmunizationCodeData Codes = new(
        new HashSet<int> { 6 }, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>());

    private static ImmunizationFacts Facts() => ImmunizationFacts.Create(new ImmunizationInput(new DateTime(2026, 11, 10), 6), 42, Now, Codes);

    private static readonly PatientReference Patient = PatientReference.Create(Guid.NewGuid(), 42);

    [Fact]
    public void An_identity_needs_a_real_uuid() =>
        Assert.Throws<ClinicalCareValidationException>(() => ImmunizationIdentity.Create(Guid.Empty));

    [Fact]
    public void A_recorded_immunization_holds_its_facts_patient_and_identity_and_no_database_id_yet()
    {
        var uuid = Guid.NewGuid();

        var immunization = Immunization.Record(Patient, Facts(), 7, ImmunizationIdentity.Create(uuid), Now);

        Assert.Equal(uuid, immunization.Uuid);
        Assert.Equal(0, immunization.Id);
        Assert.Equal(Patient, immunization.Patient);
        Assert.Equal(7, immunization.ProviderId);
        Assert.Equal(Now, immunization.RecordedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((long)int.MaxValue + 1)]
    public void The_acting_provider_must_fit_the_created_by_column(long provider) =>
        Assert.Throws<ClinicalCareValidationException>(() => Immunization.Record(Patient, Facts(), provider, ImmunizationIdentity.Create(Guid.NewGuid()), Now));

    private static ImmunizationView View() => new(
        Guid.NewGuid(), Guid.NewGuid(), 42, new DateTime(2026, 11, 10, 8, 0, 0), 6, "140", "Acme", "L1", new DateTime(2027, 1, 1), 0.5m, "1", "intramuscular", "left_arm",
        "Completed", "00", "OSTUDY", new DateTime(2026, 11, 10), new DateTime(2026, 8, 6), "Joy", 7, 9, 1001, "R", "why", "note", false, new DateTime(2026, 11, 10, 8, 5, 0));

    [Fact]
    public void The_version_of_the_same_row_is_stable()
    {
        var v = View();

        Assert.Equal(v.Version(), (v with { }).Version());
    }

    [Fact]
    public void Every_owned_field_is_in_the_version_so_a_change_to_any_of_them_is_detected()
    {
        var v = View();
        var changes = new Dictionary<string, ImmunizationView>
        {
            [nameof(v.AdministeredDate)] = v with { AdministeredDate = v.AdministeredDate!.Value.AddSeconds(1) },
            [nameof(v.ImmunizationId)] = v with { ImmunizationId = 7 },
            [nameof(v.CvxCode)] = v with { CvxCode = "141" },
            [nameof(v.Manufacturer)] = v with { Manufacturer = "Other" },
            [nameof(v.LotNumber)] = v with { LotNumber = "L2" },
            [nameof(v.ExpirationDate)] = v with { ExpirationDate = new DateTime(2027, 1, 2) },
            [nameof(v.AmountAdministered)] = v with { AmountAdministered = 0.6m },
            [nameof(v.AmountUnit)] = v with { AmountUnit = "2" },
            [nameof(v.Route)] = v with { Route = "bymouth" },
            [nameof(v.AdministrationSite)] = v with { AdministrationSite = "right_arm" },
            [nameof(v.CompletionStatus)] = v with { CompletionStatus = "Refused" },
            [nameof(v.InformationSource)] = v with { InformationSource = "01" },
            [nameof(v.RefusalReason)] = v with { RefusalReason = "PATOBJ" },
            [nameof(v.EducationDate)] = v with { EducationDate = new DateTime(2026, 11, 11) },
            [nameof(v.VisDate)] = v with { VisDate = new DateTime(2026, 8, 7) },
            [nameof(v.AdministeredBy)] = v with { AdministeredBy = "Other" },
            [nameof(v.AdministeredById)] = v with { AdministeredById = 8 },
            [nameof(v.OrderingProvider)] = v with { OrderingProvider = 10 },
            [nameof(v.EncounterNumber)] = v with { EncounterNumber = 1002 },
            [nameof(v.ReasonCode)] = v with { ReasonCode = "S" },
            [nameof(v.ReasonDescription)] = v with { ReasonDescription = "else" },
            [nameof(v.Note)] = v with { Note = "changed" },
            [nameof(v.EnteredInError)] = v with { EnteredInError = true }
        };

        // Every property of the view is either listed above or deliberately outside the version: a new property that is neither fails here, so it cannot be forgotten.
        var notVersioned = new[] { nameof(v.Uuid), nameof(v.PatientUuid), nameof(v.PatientPid), nameof(v.RecordedAt), nameof(v.EncounterUuid) };
        var all = typeof(ImmunizationView).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).ToHashSet();
        Assert.Equal(all.Except(notVersioned).OrderBy(n => n), changes.Keys.OrderBy(n => n));

        foreach (var (name, changed) in changes)
            Assert.NotEqual(v.Version(), changed.Version());
    }

    [Fact]
    public void A_change_to_a_column_the_service_does_not_own_does_not_change_the_version()
    {
        var v = View();

        Assert.Equal(v.Version(), (v with { RecordedAt = new DateTime(2020, 1, 1), PatientUuid = Guid.NewGuid() }).Version());
    }

    [Fact]
    public void A_missing_value_and_an_empty_one_are_different_versions_so_clearing_a_field_is_a_change()
    {
        var v = View();

        Assert.NotEqual((v with { Note = null }).Version(), (v with { Note = "" }).Version());
    }

    [Fact]
    public void The_amount_is_versioned_by_its_value_not_its_scale()
    {
        var v = View();

        Assert.Equal((v with { AmountAdministered = 0.5m }).Version(), (v with { AmountAdministered = 0.50m }).Version());
    }
}
