using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Problems;

/// <summary>
/// The record-a-problem use case against in-memory fakes of every port: orchestration only, no database (DOM-19). What reaches the registry, what is
/// audited, and what a refusal does not do (M5c slice 1, ADR 0069).
/// </summary>
public class RecordProblemUseCaseTests
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
        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct) =>
            Task.FromResult(patient is not null && patient.Uuid == patientUuid ? patient : null);
    }

    private sealed class FakeCodes : IIssueCodeLists
    {
        public int Calls { get; private set; }

        public Task<IssueCodeData> GetCodesAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new IssueCodeData(new HashSet<int> { 0, 1, 2 }, new HashSet<int> { 0, 1, 4 }));
        }
    }

    private sealed class FakeIdentities : IProblemIdentityAllocator
    {
        public int Calls { get; private set; }

        public ProblemIdentity Allocate()
        {
            Calls++;
            return ProblemIdentity.Create(Guid.Parse("c0c0c0c0-0000-4000-8000-000000000001"));
        }
    }

    private sealed class FakeRegistry : IProblemRegistry
    {
        public Problem? Registered { get; private set; }
        public ProblemRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(Problem problem, ProblemRecordedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            Registered = problem;
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

    private sealed record Harness(
        RecordProblemUseCase UseCase, FakeIdentities Identities, FakeRegistry Registry, FakeAuditLog Log, FakeCodes Codes);

    private static Harness Build(bool patientExists = true)
    {
        var identities = new FakeIdentities();
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        var codes = new FakeCodes();
        var patient = patientExists ? PatientReference.Create(PatientUuid, 42) : null;
        return new Harness(
            new RecordProblemUseCase(new FakePatients(patient), codes, identities, registry, log, new FixedClock(Now)), identities, registry, log, codes);
    }

    private static RecordProblemCommand Command(
        string? title = "Asthma", DateTime? begin = null, string? diagnosis = "ICD10:J45.909", int outcome = 0, long providerId = 7,
        RequestOrigin? origin = null) =>
        new(PatientUuid, title, begin ?? new DateTime(2020, 5, 1), null, diagnosis, outcome, 1, null, "note", providerId, origin);

    [Fact]
    public async Task A_valid_command_registers_the_problem_its_event_and_its_audit_row_together()
    {
        var h = Build();

        var problem = await h.UseCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Same(problem, h.Registry.Registered);
        Assert.Equal("Asthma", problem.Title);
        Assert.Equal(1, problem.Occurrence);
        Assert.Equal(h.Registry.Event!.ProblemUuid, problem.Uuid);
        Assert.Empty(h.Log.Entries);   // the success row travels with the registry call, not through the log
    }

    [Fact]
    public async Task The_recorded_time_is_the_clocks_local_now_and_the_event_carries_utc()
    {
        var h = Build();

        var problem = await h.UseCase.ExecuteAsync(Command(begin: new DateTime(2019, 1, 1)), CancellationToken.None);

        Assert.Equal(Now.DateTime, problem.RecordedAt);          // not the begin date: legacy's date column is when it was recorded
        Assert.Equal(Now.UtcDateTime, h.Registry.Event!.RecordedAtUtc);
        Assert.Equal(PatientUuid, h.Registry.Event.PatientUuid);
        Assert.Equal(42, h.Registry.Event.PatientPid);
    }

    [Fact]
    public async Task EVT_5_the_event_carries_no_clinical_content()
    {
        var h = Build();

        await h.UseCase.ExecuteAsync(Command(title: "Secret condition", diagnosis: "ICD10:Z99.9"), CancellationToken.None);

        var properties = typeof(ProblemRecordedEvent).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(properties, n => n is "Title" or "Diagnosis" or "Comments");
        Assert.DoesNotContain("Secret", h.Registry.Event!.ToString());
    }

    [Fact]
    public async Task SEC_9_the_success_row_names_the_problem_the_named_provider_and_the_origin_and_no_values()
    {
        var h = Build();
        var command = Command(providerId: 9, origin: new RequestOrigin("trace-p", "192.0.2.8"));

        var problem = await h.UseCase.ExecuteAsync(command, CancellationToken.None);

        var audit = h.Registry.Audit!;
        Assert.Equal(AuditActions.ProblemRecord, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(problem.Uuid, audit.Subject);
        Assert.Equal("user:9", audit.Actor.Subject);
        Assert.False(audit.Actor.Verified);
        Assert.Equal("trace-p", audit.CorrelationId);
        Assert.Equal("192.0.2.8", audit.SourceAddress);
        Assert.Null(audit.Details);
    }

    [Fact]
    public async Task An_unknown_patient_is_not_found_registers_nothing_and_is_audited_as_failed_against_that_patient()
    {
        var h = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => h.UseCase.ExecuteAsync(Command(providerId: 9), CancellationToken.None));

        Assert.Null(h.Registry.Registered);
        Assert.Equal(0, h.Identities.Calls);
        var row = Assert.Single(h.Log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.ProblemRecord, row.Action);
        Assert.Equal(PatientUuid, row.Subject);
        Assert.Equal("user:9", row.Actor.Subject);
    }

    [Fact]
    public async Task A_validation_failure_registers_nothing_allocates_no_identity_and_is_audited_as_failed()
    {
        var h = Build();

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => h.UseCase.ExecuteAsync(Command(title: " "), CancellationToken.None));

        Assert.Null(h.Registry.Registered);
        Assert.Equal(0, h.Identities.Calls);
        var row = Assert.Single(h.Log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Null(row.Details);   // no field values, not even names, from a refusal
    }

    [Fact]
    public async Task The_empty_patient_uuid_is_audited_not_crashed_on()
    {
        var h = Build(patientExists: false);

        await Assert.ThrowsAsync<PatientNotFoundException>(
            () => h.UseCase.ExecuteAsync(Command() with { PatientUuid = Guid.Empty }, CancellationToken.None));

        Assert.Equal(Guid.Empty, Assert.Single(h.Log.Entries).Subject);
    }

    [Fact]
    public async Task The_code_lists_are_read_once_per_request()
    {
        var h = Build();

        await h.UseCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(1, h.Codes.Calls);
    }

    [Fact]
    public async Task A_code_that_is_not_on_the_configured_list_is_a_validation_failure()
    {
        var h = Build();

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => h.UseCase.ExecuteAsync(Command(outcome: 9), CancellationToken.None));

        Assert.Contains("Outcome 9 is not a known outcome.", ex.Errors);
    }
}
