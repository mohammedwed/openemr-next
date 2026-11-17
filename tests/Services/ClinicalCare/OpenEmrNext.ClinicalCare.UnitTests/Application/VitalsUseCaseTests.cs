using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>The vitals use cases against in-memory fakes of every port: orchestration only, no database (DOM-19).</summary>
public class VitalsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("c0ffee00-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeEncounters(EncounterView? view) : IEncounterReader
    {
        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct) =>
            Task.FromResult(view is not null && view.Uuid == encounterUuid ? view : null);

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeIdentities : IVitalsIdentityAllocator
    {
        public int Calls { get; private set; }
        public VitalsIdentity Allocate() { Calls++; return VitalsIdentity.Create(Guid.Parse("d0d0d0d0-0000-4000-8000-000000000001")); }
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

    private sealed class FakeRegistry : IVitalsRegistry
    {
        public Vitals? Registered { get; private set; }
        public VitalsRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }
        public FakeAuditLog Log { get; } = new();

        public Task RegisterAsync(Vitals vitals, VitalsRecordedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            Registered = vitals;
            Event = @event;
            Audit = audit;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReader : IVitalsReader
    {
        public int Calls { get; private set; }

        public Task<VitalsView?> FindAsync(EncounterReference encounter, Guid vitalsUuid, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<VitalsView?>(null);
        }

        public Task<VitalsPage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new VitalsPage([], 0));
        }
    }

    private static EncounterView Encounter(long? number = 1001, Guid? patientUuid = null, long? pid = 42) =>
        new(EncounterUuid, number, patientUuid ?? PatientUuid, pid, "AMB", null, null, null, 0, null, null, 7, null);

    private static RecordVitalsCommand Command(VitalsInput? input = null, long providerId = 7) =>
        new(EncounterUuid, input ?? new VitalsInput(Weight: new Quantity(70m, MeasurementUnit.Kilogram), Pulse: 64), providerId);

    private static (RecordVitalsUseCase UseCase, FakeIdentities Ids, FakeRegistry Registry) Build(EncounterView? encounter)
    {
        var ids = new FakeIdentities();
        var registry = new FakeRegistry();
        return (new RecordVitalsUseCase(new FakeEncounters(encounter), ids, registry, registry.Log, new FixedClock(Now)), ids, registry);
    }

    [Fact]
    public async Task A_valid_command_registers_the_vitals_and_their_event_together()
    {
        var (useCase, _, registry) = Build(Encounter());

        var vitals = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Same(vitals, registry.Registered);
        Assert.Equal(154.323584m, vitals.WeightPounds);
        Assert.Equal(vitals.Uuid, registry.Event!.VitalsUuid);
        Assert.Equal(EncounterUuid, registry.Event.EncounterUuid);
        Assert.Equal(1001, registry.Event.EncounterNumber);
        Assert.Equal(PatientUuid, registry.Event.PatientUuid);
        Assert.Equal(42, registry.Event.PatientPid);
    }

    [Fact]
    public async Task SEC_9_the_registered_audit_row_names_the_vitals_the_named_provider_and_the_origin()
    {
        var (useCase, _, registry) = Build(Encounter());
        var command = Command(providerId: 9) with { Origin = new RequestOrigin("trace-2", "192.0.2.2") };

        var vitals = await useCase.ExecuteAsync(command, CancellationToken.None);

        var audit = registry.Audit!;
        Assert.Equal(AuditActions.VitalsRecord, audit.Action);
        Assert.Equal(vitals.Uuid, audit.Subject);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(Now.UtcDateTime, audit.AtUtc);
        Assert.Equal("user:9", audit.Actor.Subject);
        Assert.False(audit.Actor.Verified);
        Assert.Equal("trace-2", audit.CorrelationId);
        Assert.Equal("192.0.2.2", audit.SourceAddress);
    }

    [Fact]
    public async Task SEC_9_an_unknown_encounter_is_audited_as_failed_against_the_encounter_asked_about()
    {
        var (useCase, _, registry) = Build(null);
        var command = Command(providerId: 9) with { Origin = new RequestOrigin("trace-4", "192.0.2.4") };

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => useCase.ExecuteAsync(command, CancellationToken.None));

        Assert.Null(registry.Audit);
        var row = Assert.Single(registry.Log.Entries);
        Assert.Equal(AuditActions.VitalsRecord, row.Action);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(EncounterUuid, row.Subject);
        Assert.Equal("user:9", row.Actor.Subject);
        Assert.Equal("trace-4", row.CorrelationId);
    }

    [Fact]
    public async Task SEC_9_an_encounter_that_cannot_take_vitals_is_audited_as_failed()
    {
        var (useCase, _, registry) = Build(Encounter(number: null, patientUuid: Guid.Empty, pid: null));

        await Assert.ThrowsAsync<VitalsValidationException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        var row = Assert.Single(registry.Log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(EncounterUuid, row.Subject);
        Assert.Null(registry.Audit);
    }

    [Fact]
    public async Task SEC_9_a_successful_record_writes_no_separate_failed_row()
    {
        var (useCase, _, registry) = Build(Encounter());

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Empty(registry.Log.Entries);
    }

    [Fact]
    public async Task SEC_9_the_audit_row_carries_no_measurement_value()
    {
        var (useCase, _, registry) = Build(Encounter());

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        // Names or nothing (ADR 0069 #1): a recorded value must not be derivable from the trail.
        Assert.Null(registry.Audit!.Details);
    }

    [Fact]
    public async Task CV_13_the_event_names_the_measurements_taken_and_carries_none_of_their_values()
    {
        var (useCase, _, registry) = Build(Encounter());

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(["weight", "pulse"], registry.Event!.MeasurementsTaken);
        Assert.Equal(Now.UtcDateTime, registry.Event.RecordedAtUtc);
        // The record has no value-bearing member at all.
        Assert.DoesNotContain(typeof(VitalsRecordedEvent).GetProperties(), p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?));
    }

    [Fact]
    public async Task CV_1_an_unknown_encounter_is_not_found_and_nothing_is_persisted()
    {
        var (useCase, ids, registry) = Build(null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(0, ids.Calls);
        Assert.Null(registry.Registered);
    }

    [Fact]
    public async Task A_half_filled_legacy_encounter_cannot_take_vitals_and_nothing_is_persisted()
    {
        var (useCase, _, registry) = Build(Encounter(number: null, patientUuid: Guid.Empty, pid: null));

        var ex = await Assert.ThrowsAsync<VitalsValidationException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(2, ex.Errors.Count);
        Assert.Null(registry.Registered);
    }

    [Fact]
    public async Task A_rejected_recording_persists_nothing_and_allocates_no_identity()
    {
        var (useCase, ids, registry) = Build(Encounter());

        await Assert.ThrowsAsync<VitalsValidationException>(() => useCase.ExecuteAsync(Command(new VitalsInput()), CancellationToken.None));

        Assert.Equal(0, ids.Calls);
        Assert.Null(registry.Registered);
    }

    [Fact]
    public async Task Get_for_an_unknown_encounter_is_not_found()
    {
        await Assert.ThrowsAsync<EncounterNotFoundException>(() =>
            new GetVitalsUseCase(new FakeEncounters(null), new FakeReader()).ExecuteAsync(EncounterUuid, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_on_a_half_filled_encounter_finds_nothing_without_asking_the_reader()
    {
        var reader = new FakeReader();

        var view = await new GetVitalsUseCase(new FakeEncounters(Encounter(number: null)), reader)
            .ExecuteAsync(EncounterUuid, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(view);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task List_rejects_an_oversized_page_before_looking_anything_up()
    {
        var reader = new FakeReader();

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() =>
            new ListEncounterVitalsUseCase(new FakeEncounters(Encounter()), reader).ExecuteAsync(EncounterUuid, 10_000, null, CancellationToken.None));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task List_on_a_half_filled_encounter_is_an_empty_page_not_an_error()
    {
        var page = await new ListEncounterVitalsUseCase(new FakeEncounters(Encounter(pid: null)), new FakeReader())
            .ExecuteAsync(EncounterUuid, null, null, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task List_for_an_unknown_encounter_is_not_found()
    {
        await Assert.ThrowsAsync<EncounterNotFoundException>(() =>
            new ListEncounterVitalsUseCase(new FakeEncounters(null), new FakeReader()).ExecuteAsync(EncounterUuid, null, null, CancellationToken.None));
    }
}
