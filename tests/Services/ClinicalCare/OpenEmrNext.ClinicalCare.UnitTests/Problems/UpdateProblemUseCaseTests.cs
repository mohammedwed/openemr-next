using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>
/// The problem update use case against in-memory fakes of every port: orchestration only, no database (DOM-19). The order of the checks, what is written, and what is
/// audited are the behaviour under test (M5c slice 3, ADR 0068, ADR 0069).
/// </summary>
public class UpdateProblemUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid ProblemUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000002");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly DateTime Stamp = new(2026, 11, 10, 8, 0, 0);
    private static readonly ConcurrencyToken CurrentToken = ConcurrencyToken.From(Stamp);
    private static readonly DateTime Begin = new(2020, 5, 1);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeReader(ProblemView? view) : IProblemReader
    {
        public ProblemView? View { get; set; } = view;

        public Task<ProblemView?> FindAsync(Guid problemUuid, CancellationToken ct) =>
            Task.FromResult(View is not null && View.Uuid == problemUuid ? View : null);

        public Task<ProblemPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeCodes : IIssueCodeLists
    {
        public int Calls { get; private set; }

        public Task<IssueCodeData> GetCodesAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new IssueCodeData(new HashSet<int> { 0, 1, 2 }, new HashSet<int> { 0, 1, 4 }, new HashSet<string> { "confirmed", "refuted" }));
        }
    }

    private sealed class FakeUpdater(FakeReader reader) : IProblemUpdater
    {
        public UpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, new DateTime(2026, 11, 17, 9, 30, 0));
        public ProblemUpdateRequest? Request { get; private set; }

        public Task<UpdateOutcome> UpdateAsync(ProblemUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            if (Outcome.Status == UpdateStatus.Updated && reader.View is { } v)
            {
                var c = request.Change;
                reader.View = v with
                {
                    Title = c.Title, BeginDate = c.BeginDate, EndDate = c.EndDate, Diagnosis = c.Diagnosis, Outcome = c.Outcome, Occurrence = c.Occurrence,
                    ReferredBy = c.ReferredBy, Comments = c.Comments, Verification = c.Verification, LastModified = Outcome.Stamp
                };
            }

            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task AppendAsync(AuditEntry entry, CancellationToken ct)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private static ProblemView Stored(DateTime? lastModified = null) =>
        new(ProblemUuid, PatientUuid, 42, "Asthma", Begin, null, "ICD10:J45.909", 0, 1, null, "note", null, null, lastModified ?? Stamp, "confirmed");

    private static (UpdateProblemUseCase UseCase, FakeReader Reader, FakeUpdater Updater, FakeAuditLog Log, FakeCodes Codes) Build(ProblemView? stored)
    {
        var reader = new FakeReader(stored);
        var updater = new FakeUpdater(reader);
        var log = new FakeAuditLog();
        var codes = new FakeCodes();
        return (new UpdateProblemUseCase(reader, codes, updater, log, new FixedClock(Now)), reader, updater, log, codes);
    }

    /// <summary>The stored problem unchanged, with the named fields replaced.</summary>
    private static UpdateProblemCommand Command(
        IReadOnlyList<ConcurrencyToken>? offered = null, string? title = "Asthma", DateTime? end = null, int outcome = 0, string? comments = "note",
        string? verification = "confirmed", RequestOrigin? origin = null) =>
        new(ProblemUuid, offered ?? [CurrentToken], title, Begin, end, "ICD10:J45.909", outcome, 1, null, comments, verification, origin);

    [Fact]
    public async Task A_change_is_written_with_its_event_and_audit_row_and_the_caller_gets_the_new_state()
    {
        var (useCase, _, updater, log, _) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(comments: "changed"), CancellationToken.None);

        Assert.Equal(["comments"], result.ChangedFields);
        Assert.Equal("changed", result.Problem.Comments);
        var request = updater.Request!;
        Assert.Equal(CurrentToken, request.Expected);
        Assert.Equal(["comments"], request.Event.ChangedFields);
        Assert.Equal(ProblemUuid, request.Event.ProblemUuid);
        Assert.Equal(PatientUuid, request.Event.PatientUuid);
        Assert.Equal(Now.UtcDateTime, request.Event.UpdatedAtUtc);
        Assert.Empty(log.Entries);   // the success row travels with the update, not through the log
    }

    [Fact]
    public async Task Resolving_a_problem_is_an_update_that_sets_the_end_date_and_outcome()
    {
        var (useCase, _, updater, _, _) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(end: new DateTime(2022, 1, 1), outcome: 1), CancellationToken.None);

        Assert.Equal(["endDate", "outcome"], result.ChangedFields);
        Assert.Equal(new DateTime(2022, 1, 1), updater.Request!.Change.EndDate);
        Assert.Equal(1, updater.Request.Change.Outcome);
    }

    [Fact]
    public async Task SEC_9_the_audit_row_names_the_problem_the_changed_field_names_no_values_and_an_unknown_actor()
    {
        var (useCase, _, updater, _, _) = Build(Stored());

        await useCase.ExecuteAsync(Command(comments: "secret detail", origin: new RequestOrigin("trace-up", "192.0.2.17")), CancellationToken.None);

        var audit = updater.Request!.Audit;
        Assert.Equal(AuditActions.ProblemUpdate, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(ProblemUuid, audit.Subject);
        Assert.Equal("""["comments"]""", audit.Details);
        Assert.DoesNotContain("secret", audit.Details);
        Assert.Equal(AuditActor.Unknown, audit.Actor);
        Assert.Equal("trace-up", audit.CorrelationId);
        Assert.Equal("192.0.2.17", audit.SourceAddress);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing_because_nothing_happened()
    {
        var (useCase, _, updater, log, _) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Empty(result.ChangedFields);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
        Assert.Equal(Stamp, result.Problem.LastModified);
    }

    [Fact]
    public async Task A_stale_tag_is_refused_before_anything_is_validated_and_audited_as_failed()
    {
        var current = new DateTime(2026, 11, 12, 10, 0, 0);
        var (useCase, _, updater, log, codes) = Build(Stored(lastModified: current));

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(title: " "), CancellationToken.None));   // invalid too

        Assert.Equal(current, ex.CurrentStamp);
        Assert.Null(updater.Request);
        Assert.Equal(0, codes.Calls);   // the precondition is judged before the code lists are even read
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.ProblemUpdate, row.Action);
        Assert.Equal(ProblemUuid, row.Subject);
    }

    [Fact]
    public async Task No_tags_a_row_with_no_stamp_and_a_list_with_a_stale_tag_are_all_conflicts()
    {
        var (useCase, reader, updater, _, _) = Build(Stored());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(offered: []), CancellationToken.None));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(offered: [ConcurrencyToken.From(Stamp.AddDays(-1))]), CancellationToken.None));
        reader.View = Stored() with { LastModified = null };
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(comments: "x"), CancellationToken.None));

        Assert.Null(ex.CurrentStamp);
        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task Any_one_matching_tag_in_a_list_is_enough()
    {
        var (useCase, _, updater, _, _) = Build(Stored());

        await useCase.ExecuteAsync(Command(offered: [ConcurrencyToken.From(Stamp.AddDays(-1)), CurrentToken], comments: "x"), CancellationToken.None);

        Assert.NotNull(updater.Request);
    }

    [Fact]
    public async Task An_unknown_problem_is_not_found_and_audited_as_failed()
    {
        var (useCase, _, updater, log, _) = Build(null);

        await Assert.ThrowsAsync<ProblemNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(updater.Request);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task A_validation_failure_writes_nothing_and_is_audited_as_failed_with_no_values()
    {
        var (useCase, _, updater, log, _) = Build(Stored());

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(() => useCase.ExecuteAsync(Command(verification: "differential"), CancellationToken.None));

        Assert.Contains("Verification 'differential' is not a known verification.", ex.Errors);
        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Null(row.Details);
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_carries_the_rows_current_stamp_and_is_audited_as_failed()
    {
        var (useCase, _, updater, log, _) = Build(Stored());
        var winner = new DateTime(2026, 11, 17, 9, 29, 59);
        updater.Outcome = new UpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(comments: "x"), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentStamp);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task A_problem_that_vanished_while_the_update_ran_is_not_found()
    {
        var (useCase, _, updater, _, _) = Build(Stored());
        updater.Outcome = new UpdateOutcome(UpdateStatus.NotFound, null);

        await Assert.ThrowsAsync<ProblemNotFoundException>(() => useCase.ExecuteAsync(Command(comments: "x"), CancellationToken.None));
    }
}
