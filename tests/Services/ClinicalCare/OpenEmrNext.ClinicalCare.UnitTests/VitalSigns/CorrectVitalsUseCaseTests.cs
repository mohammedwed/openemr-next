using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.VitalSigns;

/// <summary>The vitals correction use case against in-memory fakes of every port: orchestration only, no database (DOM-19); the order of the checks and what is written and audited.</summary>
public class CorrectVitalsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid VitalsUuid = Guid.Parse("d0d0d0d0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly DateTime Stamp = new(2026, 11, 10, 8, 0, 0);
    private static readonly ConcurrencyToken CurrentToken = ConcurrencyToken.From(Stamp);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeEncounters(EncounterView? view) : IEncounterReader
    {
        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct) =>
            Task.FromResult(view is not null && view.Uuid == encounterUuid ? view : null);

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeVitals(VitalsView? view) : IVitalsReader
    {
        public VitalsView? View { get; set; } = view;
        public EncounterReference? AskedAbout { get; private set; }

        public Task<VitalsView?> FindAsync(EncounterReference encounter, Guid vitalsUuid, CancellationToken ct)
        {
            AskedAbout = encounter;
            return Task.FromResult(View is not null && View.Uuid == vitalsUuid ? View : null);
        }

        public Task<VitalsPage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct) => throw new NotSupportedException();

        // The patient-level reads (the FHIR Observation, ADR 0079) are not what this test is about.
        public Task<PatientVitals?> FindAnywhereAsync(Guid vitalsUuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<PatientVitals>> ListForPatientAsync(PatientReference patient, int max, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeUpdater(FakeVitals reader) : IVitalsUpdater
    {
        public UpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, new DateTime(2026, 11, 17, 9, 30, 0));
        public VitalsUpdateRequest? Request { get; private set; }

        public Task<UpdateOutcome> UpdateAsync(VitalsUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            if (Outcome.Status == UpdateStatus.Updated && reader.View is { } v)
            {
                var c = request.Change;
                reader.View = v with
                {
                    WeightPounds = c.WeightPounds, HeightInches = c.HeightInches, TemperatureFahrenheit = c.TemperatureFahrenheit, TemperatureMethod = c.TemperatureMethod,
                    Systolic = c.Systolic, Diastolic = c.Diastolic, Pulse = c.Pulse, Respiration = c.Respiration, OxygenSaturation = c.OxygenSaturation,
                    WaistInches = c.WaistInches, HeadInches = c.HeadInches, Bmi = c.Bmi, BmiStatus = c.BmiStatus, Note = c.Note, LastUpdated = Outcome.Stamp
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

    private static EncounterView Encounter(long? number = 1001, Guid? patientUuid = null, long? pid = 42) =>
        new(EncounterUuid, number, patientUuid ?? PatientUuid, pid, "AMB", null, null, null, 0, null, null, 7, null);

    private static VitalsView Stored(DateTime? lastUpdated = null) =>
        new(VitalsUuid, null, "dr_who", 154.323584m, 68.897638m, null, null, 120, 80, 64, null, null, null, null, 22.85m, "Normal", "calm", lastUpdated ?? Stamp);

    private static (CorrectVitalsUseCase UseCase, FakeVitals Vitals, FakeUpdater Updater, FakeAuditLog Log) Build(EncounterView? encounter, VitalsView? stored)
    {
        var vitals = new FakeVitals(stored);
        var updater = new FakeUpdater(vitals);
        var log = new FakeAuditLog();
        return (new CorrectVitalsUseCase(new FakeEncounters(encounter), vitals, updater, log, new FixedClock(Now)), vitals, updater, log);
    }

    /// <summary>The stored vitals unchanged unless a parameter says otherwise.</summary>
    private static CorrectVitalsCommand Command(
        IReadOnlyList<ConcurrencyToken>? offered = null, int? pulse = 64, int? systolic = 120, string? note = "calm", Quantity? weight = null, RequestOrigin? origin = null) =>
        new(EncounterUuid, VitalsUuid, offered ?? [CurrentToken],
            new VitalsInput(
                Weight: weight ?? new Quantity(154.323584m, MeasurementUnit.Pound), Height: new Quantity(68.897638m, MeasurementUnit.Inch),
                Systolic: systolic, Diastolic: 80, Pulse: pulse, Note: note), origin);

    [Fact]
    public async Task A_correction_is_written_with_its_event_and_audit_row_and_the_caller_gets_the_new_state()
    {
        var (useCase, _, updater, log) = Build(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Command(pulse: 72, note: "rechecked"), CancellationToken.None);

        Assert.Equal(["pulse", "note"], result.ChangedMeasurements);
        Assert.Equal(72, result.Vitals.Pulse);
        var request = updater.Request!;
        Assert.Equal(CurrentToken, request.Expected);
        Assert.Equal(1001, request.EncounterNumber);
        Assert.Equal(42, request.PatientPid);
        Assert.Equal(["pulse", "note"], request.Event.ChangedMeasurements);
        Assert.Equal(PatientUuid, request.Event.PatientUuid);
        Assert.Equal(Now.UtcDateTime, request.Event.CorrectedAtUtc);
        Assert.Empty(log.Entries);   // the success row travels with the update, not through the log
    }

    [Fact]
    public async Task SEC_9_the_audit_row_names_the_vitals_the_changed_measurements_no_values_and_an_unknown_actor()
    {
        var (useCase, _, updater, _) = Build(Encounter(), Stored());

        await useCase.ExecuteAsync(Command(pulse: 199, origin: new RequestOrigin("trace-v", "192.0.2.31")), CancellationToken.None);

        var audit = updater.Request!.Audit;
        Assert.Equal(AuditActions.VitalsCorrect, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(VitalsUuid, audit.Subject);
        Assert.Equal("""["pulse"]""", audit.Details);
        Assert.DoesNotContain("199", audit.Details);
        Assert.Equal(AuditActor.Unknown, audit.Actor);
        Assert.Equal("trace-v", audit.CorrelationId);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing()
    {
        var (useCase, _, updater, log) = Build(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Empty(result.ChangedMeasurements);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
        Assert.Equal(Stamp, result.Vitals.LastUpdated);
    }

    [Fact]
    public async Task Removing_a_reading_is_a_change_and_the_bmi_is_not_listed_because_it_is_derived()
    {
        var (useCase, _, updater, _) = Build(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(
            new CorrectVitalsCommand(EncounterUuid, VitalsUuid, [CurrentToken], new VitalsInput(Weight: new Quantity(154.323584m, MeasurementUnit.Pound), Pulse: 64, Systolic: 120, Diastolic: 80, Note: "calm")),
            CancellationToken.None);

        Assert.Equal(["height"], result.ChangedMeasurements);
        Assert.Null(updater.Request!.Change.Bmi);
    }

    [Fact]
    public async Task A_stale_tag_is_refused_before_anything_is_validated_and_audited_as_failed()
    {
        var current = new DateTime(2026, 11, 12, 10, 0, 0);
        var (useCase, _, updater, log) = Build(Encounter(), Stored(lastUpdated: current));

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => useCase.ExecuteAsync(Command(weight: new Quantity(0m, MeasurementUnit.Pound)), CancellationToken.None));   // invalid too

        Assert.Equal(current, ex.CurrentStamp);
        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.VitalsCorrect, row.Action);
        Assert.Equal(VitalsUuid, row.Subject);
    }

    [Fact]
    public async Task No_tags_a_row_with_no_stamp_and_a_stale_list_are_all_conflicts()
    {
        var (useCase, vitals, updater, _) = Build(Encounter(), Stored());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(offered: [], pulse: 70), CancellationToken.None));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => useCase.ExecuteAsync(Command(offered: [ConcurrencyToken.From(Stamp.AddDays(-1))], pulse: 70), CancellationToken.None));
        vitals.View = Stored() with { LastUpdated = null };
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(pulse: 70), CancellationToken.None));

        Assert.Null(ex.CurrentStamp);
        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task An_unknown_encounter_or_unknown_vitals_is_not_found_and_audited_as_failed()
    {
        var noEncounter = Build(null, Stored());
        var noVitals = Build(Encounter(), null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => noEncounter.UseCase.ExecuteAsync(Command(), CancellationToken.None));
        await Assert.ThrowsAsync<VitalsNotFoundException>(() => noVitals.UseCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(AuditOutcome.Failed, Assert.Single(noEncounter.Log.Entries).Outcome);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(noVitals.Log.Entries).Outcome);
        Assert.Null(noVitals.Updater.Request);
    }

    [Fact]
    public async Task An_encounter_that_cannot_own_vitals_has_none_to_correct()
    {
        var (useCase, vitals, updater, _) = Build(Encounter(number: null), Stored());

        await Assert.ThrowsAsync<VitalsNotFoundException>(() => useCase.ExecuteAsync(Command(pulse: 70), CancellationToken.None));

        Assert.Null(vitals.AskedAbout);   // the reader is not even asked
        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task The_vitals_are_looked_up_through_their_own_encounter()
    {
        var (useCase, vitals, _, _) = Build(Encounter(), Stored());

        await useCase.ExecuteAsync(Command(pulse: 70), CancellationToken.None);

        Assert.Equal(EncounterUuid, vitals.AskedAbout!.Uuid);
        Assert.Equal(1001, vitals.AskedAbout.Number);
    }

    [Fact]
    public async Task A_validation_failure_writes_nothing_and_is_audited_as_failed_with_no_values()
    {
        var (useCase, _, updater, log) = Build(Encounter(), Stored());

        var ex = await Assert.ThrowsAsync<VitalsValidationException>(
            () => useCase.ExecuteAsync(Command(weight: new Quantity(0m, MeasurementUnit.Pound)), CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.StartsWith("Weight must be", StringComparison.Ordinal));
        Assert.Null(updater.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Null(row.Details);
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_and_a_vanished_row_are_reported_as_such()
    {
        var (useCase, _, updater, log) = Build(Encounter(), Stored());
        var winner = new DateTime(2026, 11, 17, 9, 29, 59);
        updater.Outcome = new UpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Command(pulse: 70), CancellationToken.None));
        updater.Outcome = new UpdateOutcome(UpdateStatus.NotFound, null);
        await Assert.ThrowsAsync<VitalsNotFoundException>(() => useCase.ExecuteAsync(Command(pulse: 70), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentStamp);
        Assert.Equal(2, log.Entries.Count);
    }
}
