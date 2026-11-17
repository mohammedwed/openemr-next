using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>
/// The use case against in-memory fakes of every port: orchestration only, no database (DOM-19). Rule ids in
/// the names tie each test to docs/clinical-care/01-create-encounter-slice.md section 3.
/// </summary>
public class CreateEncounterUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakePatients(PatientReference? patient) : IPatientDirectory
    {
        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct) =>
            Task.FromResult(patient is not null && patient.Uuid == patientUuid ? patient : null);
    }

    private sealed class FakeCodeLists : IEncounterCodeLists
    {
        public Task<IReadOnlyList<EncounterClassOption>> GetEncounterClassesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EncounterClassOption>>(
                [new("AMB", "Outpatient", true), new("EMER", "Emergency Dept", false)]);
    }

    private sealed class FakeFacilities : IFacilityDirectory
    {
        public Task<FacilityOption?> FindAsync(int facilityId, CancellationToken ct) =>
            Task.FromResult<FacilityOption?>(facilityId == 3 ? new FacilityOption(3, "Main Clinic", 11) : null);
    }

    private sealed class FakeAllocator : IEncounterIdentityAllocator
    {
        public int Calls { get; private set; }

        public Task<EncounterIdentity> AllocateAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(EncounterIdentity.Create(Guid.Parse("c0ffee00-0000-4000-8000-000000000001"), 1000 + Calls));
        }
    }

    private sealed class FakeRegistry : IEncounterRegistry
    {
        public Encounter? Registered { get; private set; }
        public EncounterCreatedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(Encounter encounter, EncounterCreatedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            Registered = encounter;
            Event = @event;
            Audit = audit;
            return Task.CompletedTask;
        }
    }

    private static (CreateEncounterUseCase UseCase, FakeAllocator Allocator, FakeRegistry Registry) Build(bool patientExists = true)
    {
        var allocator = new FakeAllocator();
        var registry = new FakeRegistry();
        var patient = patientExists ? PatientReference.Create(PatientUuid, 42) : null;
        var useCase = new CreateEncounterUseCase(
            new FakePatients(patient), new FakeCodeLists(), new FakeFacilities(), allocator, registry, new FixedClock(Now));
        return (useCase, allocator, registry);
    }

    private static CreateEncounterCommand Command(
        string? classCode = null, int category = 5, int facilityId = 0, long providerId = 7) =>
        new(PatientUuid, classCode, category, null, "Cough", facilityId, null, providerId, null, null);

    [Fact]
    public async Task A_valid_command_registers_the_encounter_and_its_event_together()
    {
        var (useCase, _, registry) = Build();

        var encounter = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Same(encounter, registry.Registered);
        Assert.NotNull(registry.Event);
        Assert.Equal(encounter.Uuid, registry.Event!.EncounterUuid);
        Assert.Equal(encounter.Number, registry.Event.EncounterNumber);
    }

    [Fact]
    public async Task CC_11_the_event_is_curated_and_names_the_patient_by_uuid_and_pid()
    {
        var (useCase, _, registry) = Build();

        await useCase.ExecuteAsync(Command(classCode: "EMER"), CancellationToken.None);

        var @event = registry.Event!;
        Assert.Equal(PatientUuid, @event.PatientUuid);
        Assert.Equal(42, @event.PatientPid);
        Assert.Equal("EMER", @event.ClassCode);
        Assert.Equal(Now.UtcDateTime, @event.CreatedAtUtc);
    }

    [Fact]
    public async Task SEC_9_the_registered_audit_row_names_the_encounter_the_named_provider_and_the_origin()
    {
        var (useCase, _, registry) = Build();
        var command = Command(providerId: 9) with { Origin = new RequestOrigin("trace-1", "192.0.2.1") };

        var encounter = await useCase.ExecuteAsync(command, CancellationToken.None);

        var audit = registry.Audit!;
        Assert.Equal(AuditActions.EncounterCreate, audit.Action);
        Assert.Equal(encounter.Uuid, audit.Subject);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(Now.UtcDateTime, audit.AtUtc);
        Assert.Equal("user:9", audit.Actor.Subject);
        Assert.Equal(9, audit.Actor.UserId);
        Assert.False(audit.Actor.Verified);   // the provider is caller-supplied until the gateway forwards a session (F-CC-1)
        Assert.Equal("trace-1", audit.CorrelationId);
        Assert.Equal("192.0.2.1", audit.SourceAddress);
    }

    [Fact]
    public async Task SEC_9_a_command_with_no_origin_is_still_audited()
    {
        var (useCase, _, registry) = Build();

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.NotNull(registry.Audit);
        Assert.Null(registry.Audit!.CorrelationId);
        Assert.Null(registry.Audit.SourceAddress);
    }

    [Fact]
    public async Task SEC_9_a_refused_create_registers_no_audit_row_here()
    {
        // Refused requests are audited by a later increment (outcome 'failed'); a rejected command must never reach the registry.
        var (useCase, _, registry) = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(registry.Audit);
    }

    [Fact]
    public async Task CC_1_an_unknown_patient_is_not_found_and_nothing_is_persisted()
    {
        var (useCase, allocator, registry) = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(0, allocator.Calls);
        Assert.Null(registry.Registered);
    }

    [Fact]
    public async Task CC_5_a_rejected_request_never_takes_a_number_from_the_shared_counter()
    {
        var (useCase, allocator, registry) = Build();

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() =>
            useCase.ExecuteAsync(Command(category: 0), CancellationToken.None));

        Assert.Equal(0, allocator.Calls);
        Assert.Null(registry.Registered);
    }

    [Fact]
    public async Task CC_5_a_successful_request_takes_exactly_one_number()
    {
        var (useCase, allocator, _) = Build();

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(1, allocator.Calls);
    }

    [Fact]
    public async Task CC_3_a_blank_class_resolves_to_the_default_through_the_port()
    {
        var (useCase, _, _) = Build();

        var encounter = await useCase.ExecuteAsync(Command(classCode: null), CancellationToken.None);

        Assert.Equal("AMB", encounter.ClassCode);
    }

    [Fact]
    public async Task CC_8_the_facility_is_looked_up_through_the_port_and_copied()
    {
        var (useCase, _, _) = Build();

        var encounter = await useCase.ExecuteAsync(Command(facilityId: 3), CancellationToken.None);

        Assert.Equal("Main Clinic", encounter.FacilityName);
        Assert.Equal(11, encounter.PosCode);
    }

    [Fact]
    public async Task CC_8_an_unknown_facility_is_a_validation_error()
    {
        var (useCase, _, _) = Build();

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(() =>
            useCase.ExecuteAsync(Command(facilityId: 77), CancellationToken.None));

        Assert.Contains("Facility 77 does not exist.", ex.Errors);
    }

    [Fact]
    public async Task CC_4_the_encounter_date_is_the_clocks_local_time()
    {
        var (useCase, _, _) = Build();

        var encounter = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(Now.DateTime, encounter.Date);
    }
}
