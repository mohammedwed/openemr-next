using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Builds allergy domain objects the same way production does: through the validating factories.</summary>
internal static class AllergyTestData
{
    public static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    public static readonly AllergyCodeData Codes = new(
        new HashSet<string> { "unassigned", "hives", "nausea", "shortness_of_breath" },
        new HashSet<string> { "unassigned", "mild", "moderate", "severe" },
        new HashSet<string> { "unconfirmed", "confirmed", "refuted", "entered-in-error" });

    public static Allergy NewAllergy(
        Guid? uuid = null, long providerId = 7, string title = "Penicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = "SNOMED-CT:91936005",
        string? reaction = "hives", string? severity = "severe", string? verification = "confirmed", string? comments = "anaphylaxis", long pid = EncounterTestData.PatientPid)
    {
        var recording = AllergyRecording.Create(
            PatientReference.Create(EncounterTestData.PatientUuid, pid), title, begin, end, diagnosis, reaction, severity, verification, comments, providerId, Now, Codes);
        return Allergy.Record(recording, AllergyIdentity.Create(uuid ?? Guid.CreateVersion7()), Now);
    }

    /// <summary>The three lists a site has configured (the snapshot's values), inserted as root.</summary>
    public static async Task SeedCodeListsAsync(ClinicalCareContainerFixture fixture)
    {
        foreach (var o in new[] { "unassigned", "hives", "nausea", "shortness_of_breath" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('reaction', '{o}', 't', 1, 0)");
        foreach (var o in new[] { "unassigned", "mild", "moderate", "severe" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('severity_ccda', '{o}', 't', 1, 0)");
        foreach (var o in new[] { "unconfirmed", "confirmed", "refuted", "entered-in-error" })
            await fixture.RootExecuteAsync($"INSERT INTO list_options (list_id, option_id, title, seq, is_default) VALUES ('allergyintolerance-verification', '{o}', 't', 1, 0)");
    }
}
