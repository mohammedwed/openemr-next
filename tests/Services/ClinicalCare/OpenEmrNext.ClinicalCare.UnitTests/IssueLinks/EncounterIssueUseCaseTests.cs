using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.IssueLinks;

/// <summary>The link, unlink and list use cases against in-memory fakes of every port: orchestration only, no database (DOM-19) (M5c slice 4, ADR 0069).</summary>
public class EncounterIssueUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid IssueUuid = Guid.Parse("c0c0c0c0-0000-4000-8000-000000000009");
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

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeIssues(IssueReference? issue) : IIssueDirectory
    {
        public Task<IssueReference?> FindAsync(Guid issueUuid, CancellationToken ct) =>
            Task.FromResult(issue is not null && issue.Uuid == issueUuid ? issue : null);
    }

    private sealed class FakeIdentities : IEncounterIssueLinkIdentityAllocator
    {
        public int Calls { get; private set; }

        public EncounterIssueLinkIdentity Allocate()
        {
            Calls++;
            return EncounterIssueLinkIdentity.Create(Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001"));
        }
    }

    private sealed class FakeRegistry : IEncounterIssueRegistry
    {
        public LinkRegistration? Registration { get; set; }
        public bool Removes { get; set; } = true;
        public EncounterIssueLink? Linked { get; private set; }
        public IssueLinkedToEncounterEvent? LinkedEvent { get; private set; }
        public AuditEntry? LinkedAudit { get; private set; }
        public UnlinkRequest? Unlinked { get; private set; }

        public Task<LinkRegistration> LinkAsync(EncounterIssueLink link, IssueLinkedToEncounterEvent @event, AuditEntry audit, CancellationToken ct)
        {
            Linked = link;
            LinkedEvent = @event;
            LinkedAudit = audit;
            return Task.FromResult(Registration ?? new LinkRegistration(true, link.Uuid, link.Resolved));
        }

        public Task<bool> UnlinkAsync(UnlinkRequest request, CancellationToken ct)
        {
            Unlinked = request;
            return Task.FromResult(Removes);
        }
    }

    private sealed class FakeReader : IEncounterIssueReader
    {
        public int Calls { get; private set; }
        public (long Number, long Pid)? Asked { get; private set; }

        public Task<EncounterIssuePage> ListForEncounterAsync(long encounterNumber, long patientPid, PageRequest page, CancellationToken ct)
        {
            Calls++;
            Asked = (encounterNumber, patientPid);
            return Task.FromResult(new EncounterIssuePage([], 0));
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

    private static IssueReference Issue(long pid = 42, IssueKind kind = IssueKind.Problem) => new(IssueUuid, 17, kind, pid, "Secret condition");

    private sealed record Harness(
        LinkIssueToEncounterUseCase Link, UnlinkIssueFromEncounterUseCase Unlink, ListEncounterIssuesUseCase List,
        FakeRegistry Registry, FakeAuditLog Log, FakeIdentities Identities, FakeReader Reader);

    private static Harness Build(EncounterView? encounter, IssueReference? issue)
    {
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        var identities = new FakeIdentities();
        var reader = new FakeReader();
        var encounters = new FakeEncounters(encounter);
        var issues = new FakeIssues(issue);
        var clock = new FixedClock(Now);
        return new Harness(
            new LinkIssueToEncounterUseCase(encounters, issues, identities, registry, log, clock),
            new UnlinkIssueFromEncounterUseCase(encounters, issues, registry, log, clock),
            new ListEncounterIssuesUseCase(encounters, reader), registry, log, identities, reader);
    }

    [Fact]
    public async Task A_link_registers_the_link_its_event_and_its_audit_row_together_and_reports_it_created()
    {
        var h = Build(Encounter(), Issue(kind: IssueKind.Allergy));

        var result = await h.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, Resolved: true), CancellationToken.None);

        Assert.True(result.Created);
        Assert.True(result.Resolved);
        Assert.Equal(IssueUuid, result.IssueUuid);
        Assert.Equal(EncounterUuid, result.EncounterUuid);
        Assert.Equal(1001, h.Registry.Linked!.EncounterNumber);
        Assert.Equal(17, h.Registry.Linked.Issue.Id);
        Assert.Equal("allergy", h.Registry.LinkedEvent!.IssueKind);
        Assert.Equal(Now.UtcDateTime, h.Registry.LinkedEvent.LinkedAtUtc);
        Assert.Equal(PatientUuid, h.Registry.LinkedEvent.PatientUuid);
        Assert.Empty(h.Log.Entries);   // the success row travels with the registry call, not through the log
    }

    [Fact]
    public async Task EVT_5_the_event_and_audit_row_carry_no_title_and_no_resolved_flag()
    {
        var h = Build(Encounter(), Issue());

        await h.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, true), CancellationToken.None);

        Assert.DoesNotContain("Secret", h.Registry.LinkedEvent!.ToString());
        Assert.DoesNotContain(typeof(IssueLinkedToEncounterEvent).GetProperties(), p => p.Name is "Title" or "Resolved");
        var audit = h.Registry.LinkedAudit!;
        Assert.Equal(AuditActions.EncounterIssueLink, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(EncounterUuid, audit.Subject);
        Assert.Equal(AuditActor.Unknown, audit.Actor);
        Assert.Null(audit.Details);
    }

    [Fact]
    public async Task A_link_that_already_existed_is_reported_as_found_with_the_stored_resolved_flag_not_the_one_asked_for()
    {
        var h = Build(Encounter(), Issue());
        var existing = Guid.Parse("a2a2a2a2-0000-4000-8000-000000000002");
        h.Registry.Registration = new LinkRegistration(Created: false, existing, Resolved: false);

        var result = await h.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, Resolved: true), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(existing, result.LinkUuid);
        Assert.False(result.Resolved);
    }

    [Fact]
    public async Task An_issue_of_another_patient_is_a_validation_failure_registers_nothing_and_is_audited_as_failed_against_the_encounter()
    {
        var h = Build(Encounter(), Issue(pid: 99));

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => h.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, false), CancellationToken.None));

        Assert.Contains("The issue belongs to a different patient than the encounter.", ex.Errors);
        Assert.Null(h.Registry.Linked);
        var row = Assert.Single(h.Log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Equal(AuditActions.EncounterIssueLink, row.Action);
        Assert.Equal(EncounterUuid, row.Subject);
    }

    [Fact]
    public async Task An_unknown_encounter_or_an_unknown_issue_is_not_found_and_audited_as_failed()
    {
        var noEncounter = Build(null, Issue());
        var noIssue = Build(Encounter(), null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(
            () => noEncounter.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, false), CancellationToken.None));
        await Assert.ThrowsAsync<IssueNotFoundException>(
            () => noIssue.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, false), CancellationToken.None));

        Assert.Equal(AuditOutcome.Failed, Assert.Single(noEncounter.Log.Entries).Outcome);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(noIssue.Log.Entries).Outcome);
        Assert.Null(noEncounter.Registry.Linked);
        Assert.Null(noIssue.Registry.Linked);
        Assert.Equal(0, noIssue.Identities.Calls);   // nothing allocated for a refused request
    }

    [Fact]
    public async Task An_encounter_with_no_number_or_an_unknown_patient_cannot_be_linked_to()
    {
        var noNumber = Build(Encounter(number: null), Issue());
        var noPatient = Build(Encounter(patientUuid: Guid.Empty, pid: null), Issue());

        await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => noNumber.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, false), CancellationToken.None));
        await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => noPatient.Link.ExecuteAsync(new LinkIssueToEncounterCommand(EncounterUuid, IssueUuid, false), CancellationToken.None));
    }

    [Fact]
    public async Task An_unlink_removes_the_link_with_its_event_and_audit_row_and_reports_it_removed()
    {
        var h = Build(Encounter(), Issue(kind: IssueKind.Problem));

        var removed = await h.Unlink.ExecuteAsync(new UnlinkIssueFromEncounterCommand(EncounterUuid, IssueUuid), CancellationToken.None);

        Assert.True(removed);
        var request = h.Registry.Unlinked!;
        Assert.Equal(1001, request.EncounterNumber);
        Assert.Equal(42, request.PatientPid);
        Assert.Equal(17, request.Issue.Id);
        Assert.Equal("problem", request.Event.IssueKind);
        Assert.Equal(AuditActions.EncounterIssueUnlink, request.Audit.Action);
        Assert.Equal(AuditOutcome.Success, request.Audit.Outcome);
        Assert.Equal(EncounterUuid, request.Audit.Subject);
        Assert.Empty(h.Log.Entries);
    }

    [Fact]
    public async Task An_unlink_of_a_link_that_is_not_there_reports_false_and_writes_nothing_itself()
    {
        var h = Build(Encounter(), Issue());
        h.Registry.Removes = false;

        var removed = await h.Unlink.ExecuteAsync(new UnlinkIssueFromEncounterCommand(EncounterUuid, IssueUuid), CancellationToken.None);

        Assert.False(removed);
        Assert.Empty(h.Log.Entries);
    }

    [Fact]
    public async Task An_unlink_on_an_encounter_that_cannot_hold_links_does_not_even_ask_the_registry()
    {
        var h = Build(Encounter(number: null), Issue());

        var removed = await h.Unlink.ExecuteAsync(new UnlinkIssueFromEncounterCommand(EncounterUuid, IssueUuid), CancellationToken.None);

        Assert.False(removed);
        Assert.Null(h.Registry.Unlinked);
    }

    [Fact]
    public async Task An_unlink_for_an_unknown_encounter_or_issue_is_not_found_and_audited_as_failed()
    {
        var noEncounter = Build(null, Issue());
        var noIssue = Build(Encounter(), null);

        await Assert.ThrowsAsync<EncounterNotFoundException>(
            () => noEncounter.Unlink.ExecuteAsync(new UnlinkIssueFromEncounterCommand(EncounterUuid, IssueUuid), CancellationToken.None));
        await Assert.ThrowsAsync<IssueNotFoundException>(
            () => noIssue.Unlink.ExecuteAsync(new UnlinkIssueFromEncounterCommand(EncounterUuid, IssueUuid), CancellationToken.None));

        Assert.Equal(AuditActions.EncounterIssueUnlink, Assert.Single(noEncounter.Log.Entries).Action);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(noIssue.Log.Entries).Outcome);
    }

    [Fact]
    public async Task A_list_checks_its_bounds_before_it_looks_the_encounter_up_and_asks_the_reader_by_number_and_pid()
    {
        var h = Build(Encounter(), Issue());

        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => h.List.ExecuteAsync(EncounterUuid, 10_000, 0, CancellationToken.None));
        await h.List.ExecuteAsync(EncounterUuid, 10, 5, CancellationToken.None);

        Assert.Equal(1, h.Reader.Calls);
        Assert.Equal((1001L, 42L), h.Reader.Asked);
    }

    [Fact]
    public async Task A_list_for_an_unknown_encounter_is_not_found_and_for_one_that_cannot_hold_links_is_empty_without_asking_the_reader()
    {
        var missing = Build(null, Issue());
        var noNumber = Build(Encounter(number: null), Issue());

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => missing.List.ExecuteAsync(EncounterUuid, null, null, CancellationToken.None));
        var page = await noNumber.List.ExecuteAsync(EncounterUuid, null, null, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        Assert.Equal(0, noNumber.Reader.Calls);
    }
}
