using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>
/// The update use case against in-memory fakes of every port: orchestration only, no database (DOM-19). The order of the checks, what is
/// written, and what is audited are the behaviour under test (M5b phase 2, ADR 0068, ADR 0069).
/// </summary>
public class UpdateEncounterUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly DateTime Stamp = new(2026, 11, 10, 8, 0, 0);
    private static readonly ConcurrencyToken CurrentToken = ConcurrencyToken.From(Stamp);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeReader(EncounterView? view) : IEncounterReader
    {
        public EncounterView? View { get; set; } = view;
        public int FindCalls { get; private set; }

        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult(View is not null && View.Uuid == encounterUuid ? View : null);
        }

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCodeLists : IEncounterCodeLists
    {
        public Task<IReadOnlyList<EncounterClassOption>> GetEncounterClassesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EncounterClassOption>>([new("AMB", "Outpatient", true), new("EMER", "Emergency Dept", false)]);
    }

    private sealed class FakeFacilities : IFacilityDirectory
    {
        public Task<FacilityOption?> FindAsync(int facilityId, CancellationToken ct) =>
            Task.FromResult<FacilityOption?>(facilityId == 3 ? new FacilityOption(3, "Main Clinic", 11) : null);
    }

    private sealed class FakeUpdater(FakeReader reader) : IEncounterUpdater
    {
        public UpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, new DateTime(2026, 11, 17, 9, 30, 0));
        public EncounterUpdateRequest? Request { get; private set; }

        public Task<UpdateOutcome> UpdateAsync(EncounterUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            // What a real adapter would have done to the row, so the use case's re-read sees the new state.
            if (Outcome.Status == UpdateStatus.Updated && reader.View is { } v)
            {
                var c = request.Change;
                reader.View = v with
                {
                    ClassCode = c.ClassCode, CalendarCategoryId = c.CalendarCategoryId, Date = c.Date, OnsetDate = c.OnsetDate,
                    Reason = c.Reason, FacilityId = c.FacilityId, FacilityName = c.FacilityName, PosCode = c.PosCode,
                    ProviderId = c.ProviderId, LastUpdate = Outcome.Stamp
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

    private static EncounterView Stored(DateTime? lastUpdate = null) => new(
        EncounterUuid, 1001, PatientUuid, 42, "AMB", new DateTime(2026, 11, 10, 8, 0, 0), null, "Cough", 3, "Main Clinic", 11, 7, null,
        lastUpdate ?? Stamp, 5);

    private static (UpdateEncounterUseCase UseCase, FakeReader Reader, FakeUpdater Updater, FakeAuditLog Log) Build(EncounterView? stored)
    {
        var reader = new FakeReader(stored);
        var updater = new FakeUpdater(reader);
        var log = new FakeAuditLog();
        return (new UpdateEncounterUseCase(reader, new FakeCodeLists(), new FakeFacilities(), updater, log, new FixedClock(Now)), reader, updater, log);
    }

    /// <summary>The stored encounter unchanged, with the named fields replaced.</summary>
    private static UpdateEncounterCommand Command(
        IReadOnlyList<ConcurrencyToken>? offered = null, string? classCode = "AMB", int category = 5, DateTime? date = null,
        string? reason = "Cough", int facilityId = 3, int? pos = 11, long providerId = 7, RequestOrigin? origin = null) =>
        new(EncounterUuid, offered ?? [CurrentToken], classCode, category, date ?? new DateTime(2026, 11, 10, 8, 0, 0), null, reason,
            facilityId, pos, providerId, origin);

    [Fact]
    public async Task A_change_is_written_with_its_event_and_audit_row_and_the_caller_gets_the_new_state()
    {
        var (useCase, _, updater, log) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(reason: "Chest pain", classCode: "EMER"), CancellationToken.None);

        Assert.Equal(["classCode", "reason"], result.ChangedFields);
        Assert.Equal("Chest pain", result.Encounter.Reason);
        Assert.Equal("EMER", result.Encounter.ClassCode);
        var request = updater.Request!;
        Assert.Equal(CurrentToken, request.Expected);
        Assert.Equal(["classCode", "reason"], request.Event.ChangedFields);
        Assert.Equal(EncounterUuid, request.Event.EncounterUuid);
        Assert.Equal(1001, request.Event.EncounterNumber);
        Assert.Equal(PatientUuid, request.Event.PatientUuid);
        Assert.Equal(Now.UtcDateTime, request.Event.UpdatedAtUtc);
        Assert.Empty(log.Entries);   // the success row travels with the update, not through the log
    }

    [Fact]
    public async Task SEC_9_the_audit_row_names_the_encounter_the_changed_field_names_and_no_values_and_an_unknown_actor()
    {
        var (useCase, _, updater, _) = Build(Stored());
        var command = Command(reason: "Chest pain", origin: new RequestOrigin("trace-u", "192.0.2.7"));

        await useCase.ExecuteAsync(command, CancellationToken.None);

        var audit = updater.Request!.Audit;
        Assert.Equal(AuditActions.EncounterUpdate, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(EncounterUuid, audit.Subject);
        Assert.Equal("""["reason"]""", audit.Details);
        Assert.DoesNotContain("Chest pain", audit.Details);
        Assert.Equal(AuditActor.Unknown, audit.Actor);   // no acting user is known until identity forwarding (ADR 0069 #5)
        Assert.Equal("trace-u", audit.CorrelationId);
        Assert.Equal("192.0.2.7", audit.SourceAddress);
    }

    [Fact]
    public async Task A_facility_change_writes_the_facilitys_name_with_it()
    {
        var (useCase, _, updater, _) = Build(Stored() with { FacilityId = 0, FacilityName = null, PosCode = null });

        var result = await useCase.ExecuteAsync(Command(facilityId: 3, pos: null), CancellationToken.None);

        Assert.Contains("facilityId", result.ChangedFields);
        Assert.Equal("Main Clinic", updater.Request!.Change.FacilityName);
        Assert.Equal(11, updater.Request.Change.PosCode);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing_because_nothing_happened()
    {
        var (useCase, _, updater, log) = Build(Stored());

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Empty(result.ChangedFields);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
        Assert.Equal(Stamp, result.Encounter.LastUpdate);
    }

    [Fact]
    public async Task A_stale_tag_is_refused_before_anything_is_validated_and_audited_as_failed()
    {
        var current = new DateTime(2026, 11, 12, 10, 0, 0);
        var (useCase, _, updater, log) = Build(Stored(lastUpdate: current));
        var staleAndInvalid = Command(classCode: "NOPE");   // invalid too, but the precondition is judged first

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(staleAndInvalid, CancellationToken.None));

        Assert.Equal(current, ex.CurrentStamp);
        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.EncounterUpdate, row.Action);
        Assert.Equal(EncounterUuid, row.Subject);
    }

    [Fact]
    public async Task No_tags_at_all_is_a_conflict_because_nothing_can_match()
    {
        var (useCase, _, updater, _) = Build(Stored());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(offered: []), CancellationToken.None));

        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task Any_one_matching_tag_in_a_list_is_enough()
    {
        var (useCase, _, updater, _) = Build(Stored());
        var stale = ConcurrencyToken.From(Stamp.AddDays(-1));

        await useCase.ExecuteAsync(Command(offered: [stale, CurrentToken], reason: "x"), CancellationToken.None);

        Assert.NotNull(updater.Request);
    }

    [Fact]
    public async Task A_row_with_no_stamp_cannot_be_verified_so_it_cannot_be_updated()
    {
        var (useCase, reader, updater, _) = Build(Stored());
        reader.View = Stored() with { LastUpdate = null };

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(reason: "x"), CancellationToken.None));

        Assert.Null(ex.CurrentStamp);
        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task An_unknown_encounter_is_not_found_and_audited_as_failed()
    {
        var (useCase, _, updater, log) = Build(null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(updater.Request);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task A_validation_failure_is_audited_as_failed_with_no_values_and_writes_nothing()
    {
        var (useCase, _, updater, log) = Build(Stored());

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => useCase.ExecuteAsync(Command(classCode: "NOPE"), CancellationToken.None));

        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Null(row.Details);
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_carries_the_rows_current_stamp_and_is_audited_as_failed()
    {
        var (useCase, _, updater, log) = Build(Stored());
        var winner = new DateTime(2026, 11, 17, 9, 29, 59);
        updater.Outcome = new UpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(reason: "x"), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentStamp);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task An_encounter_deleted_while_the_update_ran_is_not_found()
    {
        var (useCase, _, updater, _) = Build(Stored());
        updater.Outcome = new UpdateOutcome(UpdateStatus.NotFound, null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => useCase.ExecuteAsync(Command(reason: "x"), CancellationToken.None));
    }
}
