using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.ClinicalNotes;

/// <summary>The rules of a clinical note (CN-1 to CN-4) and its aggregate, with no database or framework (DOM-19).</summary>
public class ClinicalNoteFactsTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    private static readonly ClinicalNoteCodeData Codes = new(
        [new("progress_note", "Progress Note", "LOINC:11506-3", true), new("retired_note", "Retired", "LOINC:1-1", false), new("no_code", "No Code", "", true)],
        [new("cardiology", "Cardiology", true), new("old_cat", "Old", false)]);

    private static ClinicalNoteInput Valid() => new(new DateTime(2026, 11, 10), "progress_note", "cardiology", "  Patient comfortable.\n Plan unchanged. ");

    private static ClinicalNoteFacts Create(ClinicalNoteInput input) => ClinicalNoteFacts.Create(input, Now, Codes);

    private static IReadOnlyList<string> ErrorsOf(ClinicalNoteInput input) => Assert.Throws<ClinicalCareValidationException>(() => Create(input)).Errors;

    [Fact]
    public void A_valid_note_keeps_its_text_exactly_as_written_and_takes_its_code_and_label_from_its_type()
    {
        var facts = Create(Valid());

        Assert.Equal("  Patient comfortable.\n Plan unchanged. ", facts.Description);   // formatting is the clinician's, not ours to trim
        Assert.Equal("progress_note", facts.Type);
        Assert.Equal("cardiology", facts.Category);
        Assert.Equal("LOINC:11506-3", facts.Code);
        Assert.Equal("Progress Note", facts.CodeText);
        Assert.Equal(new DateTime(2026, 11, 10), facts.Date);
    }

    [Fact]
    public void The_code_is_derived_from_the_type_so_it_can_never_disagree_with_it()
    {
        Assert.Equal("", Create(Valid() with { Type = "no_code" }).Code);   // an option with no LOINC code stores the empty string, as legacy does
        Assert.NotEqual(Create(Valid()).Code, Create(Valid() with { Type = "retired_note" }).Code);
    }

    [Fact]
    public void The_time_of_day_of_a_date_is_dropped_because_the_note_has_a_calendar_date() =>
        Assert.Equal(new DateTime(2026, 11, 10), Create(Valid() with { Date = new DateTime(2026, 11, 10, 23, 59, 0) }).Date);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n ")]
    public void A_note_without_text_is_refused(string? blank) =>
        Assert.Contains("A note is required.", ErrorsOf(Valid() with { Description = blank }));

    [Fact]
    public void A_note_needs_two_characters_of_substance() =>
        Assert.Contains("Description must be at least 2 characters.", ErrorsOf(Valid() with { Description = " x " }));

    [Fact]
    public void The_text_is_limited_in_bytes_not_characters()
    {
        Assert.Contains("Description must be at most 65535 bytes.", ErrorsOf(Valid() with { Description = new string('é', 40_000) }));   // 80,000 bytes
        Assert.NotNull(Create(Valid() with { Description = new string('é', 32_767) }));                                                      // 65,534 bytes
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_type_is_required(string? type) =>
        Assert.Contains("A note type is required.", ErrorsOf(Valid() with { Type = type }));

    [Theory]
    [InlineData("nonsense")]
    [InlineData("PROGRESS_NOTE")]
    public void A_type_must_be_configured_and_is_matched_exactly(string type) =>
        Assert.Contains($"Type '{type}' is not a known note type.", ErrorsOf(Valid() with { Type = type }));

    [Fact]
    public void An_inactive_type_or_category_is_still_accepted_because_legacys_own_validation_does_not_filter_on_activity()
    {
        var facts = Create(Valid() with { Type = "retired_note", Category = "old_cat" });

        Assert.Equal("retired_note", facts.Type);
        Assert.Equal("old_cat", facts.Category);
    }

    [Fact]
    public void A_category_is_optional_and_a_blank_one_is_none_but_an_unknown_one_is_refused()
    {
        Assert.Null(Create(Valid() with { Category = null }).Category);
        Assert.Null(Create(Valid() with { Category = "  " }).Category);
        Assert.Contains("Category 'nope' is not a known note category.", ErrorsOf(Valid() with { Category = "nope" }));
    }

    [Fact]
    public void A_date_is_required_and_between_1900_and_tomorrow()
    {
        Assert.Contains("A note date is required.", ErrorsOf(Valid() with { Date = null }));
        Assert.Contains(ErrorsOf(Valid() with { Date = new DateTime(1899, 12, 31) }), e => e.StartsWith("Date must be between", StringComparison.Ordinal));
        Assert.Contains(ErrorsOf(Valid() with { Date = Now.AddDays(3) }), e => e.StartsWith("Date must be between", StringComparison.Ordinal));
        Assert.NotNull(Create(Valid() with { Date = Now.AddHours(5) }));   // a day of slack covers zones
    }

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(new ClinicalNoteInput(null, "nope", "nope", " "));

        Assert.True(errors.Count >= 4, string.Join(" | ", errors));
    }

    [Fact]
    public void An_option_whose_ids_are_wider_than_the_columns_is_a_clean_message_not_a_database_error()
    {
        var wide = new ClinicalNoteCodeData([new(new string('t', 101), "Wide", "", true), new("long_code", "Long", new string('c', 256), true)], [new(new string('c', 101), "Wide", true)]);

        Assert.Contains(Assert.Throws<ClinicalCareValidationException>(() => ClinicalNoteFacts.Create(Valid() with { Type = new string('t', 101) }, Now, wide)).Errors, e => e.Contains("longer than the 100", StringComparison.Ordinal));
        Assert.Contains(Assert.Throws<ClinicalCareValidationException>(() => ClinicalNoteFacts.Create(Valid() with { Type = "long_code" }, Now, wide)).Errors, e => e.Contains("configured code", StringComparison.Ordinal));
        Assert.Contains(Assert.Throws<ClinicalCareValidationException>(() => ClinicalNoteFacts.Create(Valid() with { Category = new string('c', 101) }, Now, wide)).Errors, e => e.Contains("Category", StringComparison.Ordinal));
    }

    // Aggregate

    private static readonly EncounterReference Encounter = EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 42);

    [Fact]
    public void An_identity_needs_a_real_uuid() =>
        Assert.Throws<ClinicalCareValidationException>(() => ClinicalNoteIdentity.Create(Guid.Empty));

    [Fact]
    public void A_recorded_note_holds_its_encounter_facts_and_provider_and_no_database_id_yet()
    {
        var uuid = Guid.NewGuid();

        var note = ClinicalNote.Record(Encounter, Create(Valid()), 7, ClinicalNoteIdentity.Create(uuid));

        Assert.Equal(uuid, note.Uuid);
        Assert.Equal(0, note.Id);
        Assert.Equal(Encounter, note.Encounter);
        Assert.Equal(7, note.ProviderId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((long)int.MaxValue + 1)]
    public void The_acting_provider_must_be_a_usable_user_id(long provider) =>
        Assert.Throws<ClinicalCareValidationException>(() => ClinicalNote.Record(Encounter, Create(Valid()), provider, ClinicalNoteIdentity.Create(Guid.NewGuid())));

    [Fact]
    public void A_code_list_finds_options_by_exact_id()
    {
        Assert.NotNull(Codes.FindType("progress_note"));
        Assert.Null(Codes.FindType("Progress_Note"));
        Assert.NotNull(Codes.FindCategory("cardiology"));
        Assert.Null(Codes.FindCategory("Cardiology"));
    }

    [Fact]
    public void The_view_reads_back_what_was_derived()
    {
        var view = new ClinicalNoteView(Guid.NewGuid(), Now, "progress_note", null, "text", "LOINC:11506-3", "Progress Note", "dr_who", Now);

        Assert.Equal("LOINC:11506-3", view.Code);
    }
}
