using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Allergies;

/// <summary>The allergy use cases against in-memory fakes of every port: orchestration only, no database (DOM-19).</summary>
public class AllergyUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakePatients(PatientReference? patient) : IPatientDirectory
    {
        public int Calls { get; private set; }

        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(patient is not null && patient.Uuid == patientUuid ? patient : null);
        }
    }

    private sealed class FakeCodes : IAllergyCodeLists
    {
        public Task<AllergyCodeData> GetCodesAsync(CancellationToken ct) =>
            Task.FromResult(new AllergyCodeData(
                new HashSet<string> { "hives" }, new HashSet<string> { "mild", "severe" }, new HashSet<string> { "confirmed" }));
    }

    private sealed class FakeIdentities : IAllergyIdentityAllocator
    {
        public int Calls { get; private set; }

        public AllergyIdentity Allocate()
        {
            Calls++;
            return AllergyIdentity.Create(Guid.Parse("a0a0a0a0-0000-4000-8000-000000000001"));
        }
    }

    private sealed class FakeRegistry : IAllergyRegistry
    {
        public Allergy? Registered { get; private set; }
        public AllergyRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(Allergy allergy, AllergyRecordedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            Registered = allergy;
            Event = @event;
            Audit = audit;
            return Task.CompletedTask;
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

    private sealed class FakeReader(AllergyView? view) : IAllergyReader
    {
        public int ListCalls { get; private set; }

        public Task<AllergyView?> FindAsync(Guid allergyUuid, CancellationToken ct) =>
            Task.FromResult(view is not null && view.Uuid == allergyUuid ? view : null);

        public Task<AllergyPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
        {
            ListCalls++;
            return Task.FromResult(new AllergyPage(view is null ? [] : [view], view is null ? 0 : 1));
        }
    }

    private sealed record Harness(RecordAllergyUseCase UseCase, FakeIdentities Identities, FakeRegistry Registry, FakeAuditLog Log);

    private static Harness Build(bool patientExists = true)
    {
        var identities = new FakeIdentities();
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        var patient = patientExists ? PatientReference.Create(PatientUuid, 42) : null;
        return new Harness(new RecordAllergyUseCase(new FakePatients(patient), new FakeCodes(), identities, registry, log, new FixedClock(Now)), identities, registry, log);
    }

    private static RecordAllergyCommand Command(string? title = "Penicillin", string? severity = "severe", long providerId = 7, RequestOrigin? origin = null) =>
        new(PatientUuid, title, null, null, null, "hives", severity, "confirmed", "note", providerId, origin);

    [Fact]
    public async Task A_valid_command_registers_the_allergy_its_event_and_its_audit_row_together()
    {
        var h = Build();

        var allergy = await h.UseCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Same(allergy, h.Registry.Registered);
        Assert.Equal(allergy.Uuid, h.Registry.Event!.AllergyUuid);
        Assert.Equal(PatientUuid, h.Registry.Event.PatientUuid);
        Assert.Equal(42, h.Registry.Event.PatientPid);
        Assert.Equal(Now.UtcDateTime, h.Registry.Event.RecordedAtUtc);
        Assert.Equal(Now.DateTime, allergy.RecordedAt);
        Assert.Empty(h.Log.Entries);
    }

    [Fact]
    public async Task EVT_5_the_event_carries_no_clinical_content()
    {
        var h = Build();

        await h.UseCase.ExecuteAsync(Command(title: "Secret substance"), CancellationToken.None);

        var properties = typeof(AllergyRecordedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(properties, n => n is "Title" or "Reaction" or "Severity" or "Comments");
        Assert.DoesNotContain("Secret", h.Registry.Event!.ToString());
    }

    [Fact]
    public async Task SEC_9_the_success_row_names_the_allergy_the_named_provider_and_the_origin_and_no_values()
    {
        var h = Build();

        var allergy = await h.UseCase.ExecuteAsync(Command(providerId: 9, origin: new RequestOrigin("trace-a", "192.0.2.9")), CancellationToken.None);

        var audit = h.Registry.Audit!;
        Assert.Equal(AuditActions.AllergyRecord, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(allergy.Uuid, audit.Subject);
        Assert.Equal("user:9", audit.Actor.Subject);
        Assert.False(audit.Actor.Verified);
        Assert.Equal("trace-a", audit.CorrelationId);
        Assert.Null(audit.Details);
    }

    [Fact]
    public async Task An_unknown_patient_registers_nothing_and_is_audited_as_failed_against_that_patient()
    {
        var h = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => h.UseCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(h.Registry.Registered);
        Assert.Equal(0, h.Identities.Calls);
        var row = Assert.Single(h.Log.Entries);
        Assert.Equal(AuditActions.AllergyRecord, row.Action);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(PatientUuid, row.Subject);
    }

    [Fact]
    public async Task A_validation_failure_registers_nothing_allocates_no_identity_and_is_audited_as_failed()
    {
        var h = Build();

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(() => h.UseCase.ExecuteAsync(Command(severity: "catastrophic"), CancellationToken.None));

        Assert.Contains("Severity 'catastrophic' is not a known severity.", ex.Errors);
        Assert.Null(h.Registry.Registered);
        Assert.Equal(0, h.Identities.Calls);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(h.Log.Entries).Outcome);
    }

    [Fact]
    public async Task The_empty_patient_uuid_is_audited_not_crashed_on()
    {
        var h = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => h.UseCase.ExecuteAsync(Command() with { PatientUuid = Guid.Empty }, CancellationToken.None));

        Assert.Equal(Guid.Empty, Assert.Single(h.Log.Entries).Subject);
    }

    [Fact]
    public async Task A_get_returns_the_view_or_null_and_a_list_checks_its_bounds_before_it_looks_the_patient_up()
    {
        var view = new AllergyView(Guid.NewGuid(), PatientUuid, 42, "Penicillin", null, null, null, "hives", "severe", "confirmed", null, null, null, null);
        var get = new GetAllergyUseCase(new FakeReader(view));
        var patients = new FakePatients(PatientReference.Create(PatientUuid, 42));
        var list = new ListPatientAllergiesUseCase(patients, new FakeReader(view));

        Assert.Same(view, await get.ExecuteAsync(view.Uuid, CancellationToken.None));
        Assert.Null(await get.ExecuteAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(1, (await list.ExecuteAsync(PatientUuid, null, null, CancellationToken.None)).Total);
        var before = patients.Calls;
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => list.ExecuteAsync(PatientUuid, 10_000, 0, CancellationToken.None));
        Assert.Equal(before, patients.Calls);
    }

    [Fact]
    public async Task A_list_for_an_unknown_patient_is_not_found_and_asks_the_reader_nothing()
    {
        var reader = new FakeReader(null);
        var list = new ListPatientAllergiesUseCase(new FakePatients(null), reader);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => list.ExecuteAsync(Guid.NewGuid(), null, null, CancellationToken.None));

        Assert.Equal(0, reader.ListCalls);
    }
}
