using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Shared fixtures for the clinical note adapter and endpoint tests.</summary>
internal static class ClinicalNoteTestData
{
    public static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    public static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);

    /// <summary>The lists <see cref="SeedCodesAsync"/> writes, for building facts in-process.</summary>
    public static readonly ClinicalNoteCodeData Codes = new(
        [new("progress_note", "Progress Note", "LOINC:11506-3", true), new("nurse_note", "Nurse Note", "LOINC:34746-8", true), new("retired_note", "Retired Note", "", false)],
        [new("cardiology", "Cardiology", true), new("old_category", "Old", false)]);

    public static Task SeedCodesAsync(ClinicalCareContainerFixture fixture) =>
        fixture.RootExecuteAsync(
            """
            INSERT INTO list_options (list_id, option_id, title, seq, is_default, notes, activity) VALUES
                ('Clinical_Note_Type', 'progress_note', 'Progress Note', 10, 0, 'LOINC:11506-3', 1),
                ('Clinical_Note_Type', 'nurse_note', 'Nurse Note', 20, 0, 'LOINC:34746-8', 1),
                ('Clinical_Note_Type', 'retired_note', 'Retired Note', 30, 0, NULL, 0),
                ('Clinical_Note_Category', 'cardiology', 'Cardiology', 10, 0, 'LOINC:LP29708-2', 1),
                ('Clinical_Note_Category', 'old_category', 'Old', 20, 0, NULL, 0),
                ('other_list', 'progress_note', 'A different list must never leak in', 1, 0, NULL, 1)
            """);

    public static ClinicalNoteInput Input(string? description = "Patient comfortable.", string type = "progress_note", string? category = "cardiology", DateTime? date = null) =>
        new(date ?? new DateTime(2026, 11, 10), type, category, description);

    public static ClinicalNote New(ClinicalNoteInput? input = null, EncounterReference? encounter = null, long provider = 7, Guid? uuid = null) =>
        ClinicalNote.Record(
            encounter ?? EncounterReference.Create(Guid.NewGuid(), 1001, EncounterTestData.PatientUuid, 42),
            ClinicalNoteFacts.Create(input ?? Input(), Now, Codes), provider, ClinicalNoteIdentity.Create(uuid ?? Guid.CreateVersion7()));

    public static string Hex(Guid uuid) => $"UNHEX('{uuid:N}')";
}
