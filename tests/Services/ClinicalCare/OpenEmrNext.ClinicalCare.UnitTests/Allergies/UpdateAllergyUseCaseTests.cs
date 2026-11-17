using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Allergies;

/// <summary>The allergy update use case against in-memory fakes: the same order of checks and the same audit rules as the problem one (ADR 0068, ADR 0069).</summary>
public class UpdateAllergyUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid AllergyUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000003");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly DateTime Stamp = new(2026, 11, 10, 8, 0, 0);
    private static readonly ConcurrencyToken CurrentToken = ConcurrencyToken.From(Stamp);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeReader(AllergyView? view) : IAllergyReader
    {
        public AllergyView? View { get; set; } = view;

        public Task<AllergyView?> FindAsync(Guid allergyUuid, CancellationToken ct) =>
            Task.FromResult(View is not null && View.Uuid == allergyUuid ? View : null);

        public Task<AllergyPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeCodes : IAllergyCodeLists
    {
        public Task<AllergyCodeData> GetCodesAsync(CancellationToken ct) =>
            Task.FromResult(new AllergyCodeData(
                new HashSet<string> { "hives", "nausea" }, new HashSet<string> { "mild", "severe" }, new HashSet<string> { "confirmed", "refuted" }));
    }

    private sealed class FakeUpdater(FakeReader reader) : IAllergyUpdater
    {
        public UpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, new DateTime(2026, 11, 17, 9, 30, 0));
        public AllergyUpdateRequest? Request { get; private set; }

        public Task<UpdateOutcome> UpdateAsync(AllergyUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            if (Outcome.Status == UpdateStatus.Updated && reader.View is { } v)
            {
                var c = request.Change;
                reader.View = v with
                {
                    Title = c.Title, BeginDate = c.BeginDate, EndDate = c.EndDate, Diagnosis = c.Diagnosis, Reaction = c.Reaction, Severity = c.Severity,
                    Verification = c.Verification, Comments = c.Comments, LastModified = Outcome.Stamp
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

    private static AllergyView Stored(DateTime? lastModified = null) =>
        new(AllergyUuid, PatientUuid, 42, "Penicillin", null, null, null, "hives", "severe", "confirmed", "note", null, null, lastModified ?? Stamp);

    private static (UpdateAllergyUseCase UseCase, FakeReader Reader, FakeUpdater Updater, FakeAuditLog Log) Build(AllergyView? stored)
    {
        var reader = new FakeReader(stored);
        var updater = new FakeUpdater(reader);
        var log = new FakeAuditLog();
        return (new UpdateAllergyUseCase(reader, new FakeCodes(), updater, log, new FixedClock(Now)), reader, updater, log);
    }

    private static UpdateAllergyCommand Command(
        IReadOnlyList<ConcurrencyToken>? offered = null, DateTime? end = null, string? reaction = "hives", string? severity = "severe", RequestOrigin? origin = null) =>
        new(AllergyUuid, offered ?? [CurrentToken], "Penicillin", null, end, null, reaction, severity, "confirmed", "note", origin);

    [Fact]
    public async Task A_change_is_written_with_its_event_and_audit_row_and_the_caller_gets_the_new_state()
    {
        var (useCase, _, updater, log) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(reaction: "nausea", severity: "mild"), CancellationToken.None);

        Assert.Equal(["reaction", "severity"], result.ChangedFields);
        Assert.Equal("nausea", result.Allergy.Reaction);
        Assert.Equal(CurrentToken, updater.Request!.Expected);
        Assert.Equal(["reaction", "severity"], updater.Request.Event.ChangedFields);
        Assert.Equal(AllergyUuid, updater.Request.Event.AllergyUuid);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Ending_an_allergy_is_an_update_that_sets_the_end_date()
    {
        var (useCase, _, updater, _) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(end: new DateTime(2022, 1, 1)), CancellationToken.None);

        Assert.Equal(["endDate"], result.ChangedFields);
        Assert.Equal(new DateTime(2022, 1, 1), updater.Request!.Change.EndDate);
    }

    [Fact]
    public async Task SEC_9_the_audit_row_names_the_allergy_field_names_only_and_an_unknown_actor()
    {
        var (useCase, _, updater, _) = Build(Stored());

        await useCase.ExecuteAsync(Command(reaction: "nausea", origin: new RequestOrigin("trace-al", "192.0.2.18")), CancellationToken.None);

        var audit = updater.Request!.Audit;
        Assert.Equal(AuditActions.AllergyUpdate, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(AllergyUuid, audit.Subject);
        Assert.Equal("""["reaction"]""", audit.Details);
        Assert.DoesNotContain("nausea", audit.Details);
        Assert.Equal(AuditActor.Unknown, audit.Actor);
        Assert.Equal("trace-al", audit.CorrelationId);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing()
    {
        var (useCase, _, updater, log) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Empty(result.ChangedFields);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_stale_tag_is_refused_before_validation_and_audited_as_failed()
    {
        var (useCase, _, updater, log) = Build(Stored(lastModified: new DateTime(2026, 11, 12, 10, 0, 0)));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(reaction: "rash"), CancellationToken.None));   // invalid too

        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.AllergyUpdate, row.Action);
    }

    [Fact]
    public async Task An_unknown_allergy_is_not_found_and_a_validation_failure_is_audited_with_no_values()
    {
        var (missing, _, _, missingLog) = Build(null);
        var (useCase, _, updater, log) = Build(Stored());

        await Assert.ThrowsAsync<AllergyNotFoundException>(() => missing.ExecuteAsync(Command(), CancellationToken.None));
        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(() => useCase.ExecuteAsync(Command(reaction: "rash"), CancellationToken.None));

        Assert.Equal(AuditOutcome.Failed, Assert.Single(missingLog.Entries).Outcome);
        Assert.Contains("Reaction 'rash' is not a known reaction.", ex.Errors);
        Assert.Null(updater.Request);
        Assert.Null(Assert.Single(log.Entries).Details);
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_and_a_vanished_row_are_reported_as_such()
    {
        var (useCase, _, updater, log) = Build(Stored());
        var winner = new DateTime(2026, 11, 17, 9, 29, 59);
        updater.Outcome = new UpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(reaction: "nausea"), CancellationToken.None));
        updater.Outcome = new UpdateOutcome(UpdateStatus.NotFound, null);
        await Assert.ThrowsAsync<AllergyNotFoundException>(() => useCase.ExecuteAsync(Command(reaction: "nausea"), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentStamp);
        Assert.Equal(2, log.Entries.Count);
    }
}
