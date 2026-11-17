using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>
/// The rules <see cref="AuditEntry"/> enforces so a clinical write is never failed by its own audit row (ADR 0069): header-sourced text
/// is cut to the column, code-sourced text that cannot fit is a programming error, and the stored form holds names, never values.
/// </summary>
public class AuditEntryTests
{
    private static readonly DateTime At = new(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);
    private static readonly AuditActor Actor = new("user:7", 7, Verified: false);
    private static readonly Guid Subject = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static AuditEntry Entry(
        string action = AuditActions.EncounterUpdate, string? correlation = null, string? address = null,
        IReadOnlyList<string>? fields = null, AuditActor? actor = null, Guid? subject = null) =>
        new(At, actor ?? Actor, action, subject ?? Subject, AuditOutcome.Success, correlation, address, fields);

    [Fact]
    public void Changed_fields_are_stored_as_a_json_array_of_names()
    {
        var entry = Entry(fields: ["reason", "date"]);

        Assert.Equal("""["reason","date"]""", entry.Details);
    }

    [Fact]
    public void No_changed_fields_stores_nothing()
    {
        Assert.Null(Entry().Details);
        Assert.Null(Entry(fields: []).Details);
    }

    [Fact]
    public void Header_sourced_text_is_cut_to_the_column_instead_of_rejected()
    {
        var entry = Entry(
            correlation: new string('c', AuditEntry.MaxCorrelationLength + 50),
            address: new string('9', AuditEntry.MaxAddressLength + 50),
            actor: new AuditActor(new string('a', AuditEntry.MaxActorLength + 50), 7, false));

        Assert.Equal(AuditEntry.MaxCorrelationLength, entry.CorrelationId!.Length);
        Assert.Equal(AuditEntry.MaxAddressLength, entry.SourceAddress!.Length);
        Assert.Equal(AuditEntry.MaxActorLength, entry.Actor.Subject.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_optional_text_is_stored_as_nothing(string? blank)
    {
        var entry = Entry(correlation: blank, address: blank);

        Assert.Null(entry.CorrelationId);
        Assert.Null(entry.SourceAddress);
    }

    [Fact]
    public void A_blank_actor_subject_is_recorded_as_unknown_not_dropped()
    {
        var entry = Entry(actor: new AuditActor("  ", null, false));

        Assert.Equal("unknown", entry.Actor.Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_action_is_required(string? action) =>
        Assert.ThrowsAny<ArgumentException>(() => Entry(action: action!));

    [Fact]
    public void An_action_longer_than_its_column_is_a_programming_error() =>
        Assert.Throws<ArgumentException>(() => Entry(action: new string('x', AuditEntry.MaxActionLength + 1)));

    [Fact]
    public void A_successful_change_must_name_the_record_it_changed() =>
        Assert.Throws<ArgumentException>(() => Entry(subject: Guid.Empty));

    [Theory]
    [InlineData(AuditOutcome.Failed)]
    [InlineData(AuditOutcome.Denied)]
    public void A_refusal_may_name_a_uuid_that_does_not_exist_even_the_empty_one(AuditOutcome outcome)
    {
        // The caller chooses what a refused request is "about"; rejecting an empty uuid here would turn a 400 or 404 into a 500.
        var entry = new AuditEntry(At, Actor, AuditActions.VitalsRecord, Guid.Empty, outcome);

        Assert.Equal(Guid.Empty, entry.Subject);
    }

    [Fact]
    public void A_field_list_that_cannot_fit_the_details_column_is_a_programming_error()
    {
        var tooMany = Enumerable.Range(0, 200).Select(i => $"field_number_{i}").ToList();

        Assert.Throws<ArgumentException>(() => Entry(fields: tooMany));
    }

    [Theory]
    [InlineData(AuditOutcome.Success, "success")]
    [InlineData(AuditOutcome.Denied, "denied")]
    [InlineData(AuditOutcome.Failed, "failed")]
    public void Outcomes_are_stored_as_lower_case_words(AuditOutcome outcome, string stored)
    {
        var entry = new AuditEntry(At, Actor, AuditActions.VitalsRecord, Subject, outcome);

        Assert.Equal(stored, entry.OutcomeName);
    }

    [Fact]
    public void Every_action_verb_fits_its_column()
    {
        var verbs = typeof(AuditActions).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();

        Assert.NotEmpty(verbs);
        Assert.All(verbs, verb => Assert.InRange(verb.Length, 1, AuditEntry.MaxActionLength));
        Assert.Equal(verbs.Count, verbs.Distinct().Count());
    }
}
