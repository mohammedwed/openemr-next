using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.SoapNotes;

/// <summary>The rules of a SOAP note's sections (SN-1, SN-2) and the aggregate, with no database or framework (DOM-19).</summary>
public class SoapSectionsTests
{
    private static IReadOnlyList<string> ErrorsOf(SoapInput input) => Assert.Throws<ClinicalCareValidationException>(() => SoapSections.Create(input)).Errors;

    [Fact]
    public void A_note_with_every_section_keeps_each_exactly_as_written()
    {
        var sections = SoapSections.Create(new SoapInput("  cough\n for 3 days ", "lungs clear", "viral URI", "rest, fluids"));

        Assert.Equal("  cough\n for 3 days ", sections.Subjective);   // formatting is the clinician's, not ours to trim
        Assert.Equal("lungs clear", sections.Objective);
        Assert.Equal("viral URI", sections.Assessment);
        Assert.Equal("rest, fluids", sections.Plan);
    }

    [Fact]
    public void One_section_is_enough_and_the_others_are_absent()
    {
        var sections = SoapSections.Create(new SoapInput(Plan: "follow up in a week"));

        Assert.Null(sections.Subjective);
        Assert.Null(sections.Objective);
        Assert.Null(sections.Assessment);
        Assert.Equal("follow up in a week", sections.Plan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void A_note_with_no_text_at_all_is_refused_and_blank_means_not_written(string? blank)
    {
        Assert.Contains("At least one section of the note is required.", ErrorsOf(new SoapInput(blank, blank, blank, blank)));
        Assert.Null(SoapSections.Create(new SoapInput(blank, "ok", null, null)).Subjective);
    }

    [Theory]
    [InlineData("Subjective")]
    [InlineData("Objective")]
    [InlineData("Assessment")]
    [InlineData("Plan")]
    public void A_section_shorter_than_two_characters_of_substance_is_refused_by_name(string label)
    {
        var input = label switch
        {
            "Subjective" => new SoapInput("x"),
            "Objective" => new SoapInput(Objective: " y "),
            "Assessment" => new SoapInput(Assessment: "z"),
            _ => new SoapInput(Plan: "1")
        };

        Assert.Contains($"{label} must be at least 2 characters.", ErrorsOf(input));
    }

    [Fact]
    public void Two_characters_are_enough() => Assert.Equal("ok", SoapSections.Create(new SoapInput("ok")).Subjective);

    [Fact]
    public void A_section_is_limited_in_bytes_not_characters()
    {
        var multiByte = new string('é', 40_000);   // 40,000 characters, 80,000 bytes: fits a character count, not the column

        var errors = ErrorsOf(new SoapInput(Subjective: multiByte, Plan: multiByte));

        Assert.Contains("Subjective must be at most 65535 bytes.", errors);
        Assert.Contains("Plan must be at most 65535 bytes.", errors);
        Assert.Equal(65_534, System.Text.Encoding.UTF8.GetByteCount(SoapSections.Create(new SoapInput(Objective: new string('é', 32_767))).Objective!));
        Assert.NotNull(SoapSections.Create(new SoapInput(Objective: new string('a', 65_535))));
    }

    [Fact]
    public void Every_violation_is_reported_together_and_a_refused_section_is_not_also_called_empty()
    {
        var errors = ErrorsOf(new SoapInput("x", new string('a', 70_000), "y"));

        Assert.Equal(3, errors.Count);
        Assert.DoesNotContain("At least one section of the note is required.", errors);
    }

    // The aggregate and its read model

    private static readonly Guid EncounterUuid = Guid.NewGuid();
    private static readonly EncounterReference Encounter = EncounterReference.Create(EncounterUuid, 1001, Guid.NewGuid(), 42);
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);

    [Fact]
    public void A_recorded_note_holds_its_encounter_sections_and_provider_and_no_database_id_yet()
    {
        var note = SoapNote.Record(Encounter, SoapSections.Create(new SoapInput("ok")), 7, Now);

        Assert.Equal(0, note.Id);
        Assert.Equal(Encounter, note.Encounter);
        Assert.Equal(7, note.ProviderId);
        Assert.Equal(Now, note.RecordedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((long)int.MaxValue + 1)]
    public void The_acting_provider_must_be_a_usable_user_id(long provider) =>
        Assert.Throws<ClinicalCareValidationException>(() => SoapNote.Record(Encounter, SoapSections.Create(new SoapInput("ok")), provider, Now));

    [Fact]
    public void Every_section_is_in_the_version_and_who_wrote_it_and_when_are_not()
    {
        var v = new SoapNoteView(1, Now, "dr_who", "s", "o", "a", "p");

        Assert.NotEqual(v.Version(), (v with { Subjective = "x" }).Version());
        Assert.NotEqual(v.Version(), (v with { Objective = "x" }).Version());
        Assert.NotEqual(v.Version(), (v with { Assessment = "x" }).Version());
        Assert.NotEqual(v.Version(), (v with { Plan = "x" }).Version());
        Assert.Equal(v.Version(), (v with { Date = Now.AddDays(1), RecordedBy = "someone else", Id = 2 }).Version());

        // Every property is either one of the four sections or deliberately outside the version: a new one that is neither fails here.
        var outside = new[] { nameof(v.Id), nameof(v.Date), nameof(v.RecordedBy) };
        var sections = new[] { nameof(v.Subjective), nameof(v.Objective), nameof(v.Assessment), nameof(v.Plan) };
        var all = typeof(SoapNoteView).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).Order();
        Assert.Equal(outside.Concat(sections).Order(), all);
    }

    [Fact]
    public void A_missing_section_and_an_empty_one_are_different_versions_so_clearing_is_a_change()
    {
        var v = new SoapNoteView(1, Now, null, "s", null, null, null);

        Assert.NotEqual(v.Version(), (v with { Objective = "" }).Version());
    }
}
