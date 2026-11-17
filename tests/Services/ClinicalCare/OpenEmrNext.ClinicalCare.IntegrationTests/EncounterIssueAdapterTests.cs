using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The issue-link write adapter against a real MariaDB, as the production principal (LEG-2, ADR 0070): the <c>issue_encounter</c> row legacy would write, idempotence on the
/// table's unique key, atomicity with the event and the audit row, and the decision not to touch <c>uuid_registry</c> on unlink.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterIssueRegistryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid IssueUuid = Guid.Parse("c0c0c0c0-0000-4000-8000-000000000009");

    private long _issueRowId;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.RootExecuteAsync("DELETE FROM issue_encounter");
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, pid, activity) VALUES (UNHEX('{IssueUuid:N}'), 'medical_problem', 'Asthma', 42, 1)");
        _issueRowId = await fixture.RootScalarAsync<long>("SELECT id FROM lists");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private IssueReference Issue(IssueKind kind = IssueKind.Problem) => new(IssueUuid, _issueRowId, kind, 42, "Asthma");

    private EncounterIssueLink NewLink(bool resolved = false, long number = 1001, Guid? uuid = null) =>
        EncounterIssueLink.Create(EncounterUuid, number, EncounterTestData.PatientUuid, 42, Issue(), resolved, EncounterIssueLinkIdentity.Create(uuid ?? Guid.CreateVersion7()));

    private static IssueLinkedToEncounterEvent EventFor(EncounterIssueLink l) =>
        new(l.Uuid, l.EncounterUuid, l.EncounterNumber, l.Issue.Uuid, "problem", l.Patient.Uuid, l.Patient.Pid, Now.ToUniversalTime());

    private static AuditEntry AuditFor() =>
        new(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.EncounterIssueLink, EncounterUuid, AuditOutcome.Success, "corr-link", "10.5.5.5");

    private async Task<LinkRegistration> LinkAsync(EncounterIssueLink link)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfEncounterIssueRegistry(db, new EfAuditLog(db)).LinkAsync(link, EventFor(link), AuditFor(), CancellationToken.None);
    }

    private async Task<bool> UnlinkAsync(long number = 1001, IssueReference? issue = null)
    {
        await using var db = fixture.CreateDbContext();
        var request = new UnlinkRequest(
            EncounterUuid, number, 42, issue ?? Issue(),
            new IssueUnlinkedFromEncounterEvent(EncounterUuid, number, IssueUuid, "problem", EncounterTestData.PatientUuid, 42, Now.ToUniversalTime()),
            new AuditEntry(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.EncounterIssueUnlink, EncounterUuid, AuditOutcome.Success));
        return await new EfEncounterIssueRegistry(db, new EfAuditLog(db)).UnlinkAsync(request, CancellationToken.None);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    [Fact]
    public async Task A_link_writes_the_issue_encounter_row_with_legacy_shaped_values_and_leaves_the_actor_columns_null()
    {
        var link = NewLink(resolved: true);

        var registration = await LinkAsync(link);

        Assert.True(registration.Created);
        Assert.Equal(link.Uuid, registration.LinkUuid);
        Assert.True(registration.Resolved);
        Assert.True(link.Id > 0);
        Assert.Equal(42L, await fixture.RootScalarAsync<long>("SELECT pid FROM issue_encounter"));
        Assert.Equal(_issueRowId, await fixture.RootScalarAsync<long>("SELECT list_id FROM issue_encounter"));
        Assert.Equal(1001L, await fixture.RootScalarAsync<long>("SELECT encounter FROM issue_encounter"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT resolved FROM issue_encounter"));
        Assert.Null(await fixture.RootScalarAsync<long?>("SELECT created_by FROM issue_encounter"));   // the actor is unknown, so no user id is invented
        Assert.Null(await fixture.RootScalarAsync<long?>("SELECT updated_by FROM issue_encounter"));
        Assert.Equal(link.Uuid.ToString("N").ToUpperInvariant(), await fixture.RootScalarAsync<string>("SELECT HEX(uuid) FROM issue_encounter"));
    }

    [Fact]
    public async Task A_link_registers_its_uuid_writes_its_event_and_its_audit_row_in_one_commit()
    {
        var link = NewLink();

        await LinkAsync(link);

        Assert.Equal(1L, await Count("uuid_registry", $"table_name = 'issue_encounter' AND HEX(uuid) = '{link.Uuid.ToString("N").ToUpperInvariant()}'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'IssueLinkedToEncounter'");
        Assert.DoesNotContain("Asthma", payload);
        Assert.Equal("encounter.issue.link", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(EncounterUuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
    }

    [Fact]
    public async Task Linking_twice_is_idempotent_it_returns_the_stored_link_keeps_its_resolved_flag_and_writes_nothing_more()
    {
        var first = NewLink(resolved: false);
        await LinkAsync(first);

        var again = await LinkAsync(NewLink(resolved: true));   // a different uuid and a different resolved flag, for the same patient, issue and encounter

        Assert.False(again.Created);
        Assert.Equal(first.Uuid, again.LinkUuid);
        Assert.False(again.Resolved);   // a link is never updated
        Assert.Equal(1L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("clinical_care_outbox"));
        Assert.Equal(1L, await Count("clinical_care_audit"));
        Assert.Equal(1L, await Count("uuid_registry", "table_name = 'issue_encounter'"));
    }

    [Fact]
    public async Task A_link_legacy_made_first_is_returned_as_it_is_with_its_own_actor_and_no_second_row()
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO issue_encounter (uuid, pid, list_id, encounter, resolved, created_by, updated_by) VALUES (UNHEX('{Guid.NewGuid():N}'), 42, {_issueRowId}, 1001, 1, 5, 5)");

        var registration = await LinkAsync(NewLink(resolved: false));

        Assert.False(registration.Created);
        Assert.True(registration.Resolved);
        Assert.Equal(1L, await Count("issue_encounter"));
        Assert.Equal(5L, await fixture.RootScalarAsync<long>("SELECT created_by FROM issue_encounter"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task A_legacy_link_row_with_no_uuid_is_returned_with_no_link_uuid()
    {
        await fixture.RootExecuteAsync($"INSERT INTO issue_encounter (uuid, pid, list_id, encounter, resolved) VALUES (NULL, 42, {_issueRowId}, 1001, 0)");

        var registration = await LinkAsync(NewLink());

        Assert.False(registration.Created);
        Assert.Null(registration.LinkUuid);
    }

    [Fact]
    public async Task The_same_issue_on_a_different_encounter_is_a_different_link()
    {
        await LinkAsync(NewLink(number: 1001));
        await LinkAsync(NewLink(number: 1002));

        Assert.Equal(2L, await Count("issue_encounter"));
    }

    [Fact]
    public async Task QLT_12_a_failure_part_way_through_leaves_no_partial_link()
    {
        var link = NewLink();
        await fixture.RootExecuteAsync($"INSERT INTO uuid_registry (uuid, table_name) VALUES (UNHEX('{link.Uuid:N}'), 'issue_encounter')");

        await Assert.ThrowsAnyAsync<Exception>(() => LinkAsync(link));

        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0, link.Id);
    }

    [Fact]
    public async Task SEC_9_a_link_cannot_commit_without_its_audit_row()
    {
        var link = NewLink();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfEncounterIssueRegistry(db, new ThrowingAuditLog()).LinkAsync(link, EventFor(link), AuditFor(), CancellationToken.None));

        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(0L, await Count("uuid_registry", "table_name = 'issue_encounter'"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0, link.Id);
    }

    [Fact]
    public async Task An_unlink_removes_the_link_and_writes_its_event_and_its_audit_row_and_leaves_the_issue_untouched()
    {
        await LinkAsync(NewLink());
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");

        var removed = await UnlinkAsync();

        Assert.True(removed);
        Assert.Equal(0L, await Count("issue_encounter"));
        Assert.Equal(1L, await Count("lists", "type = 'medical_problem'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'IssueUnlinkedFromEncounter'"));
        Assert.Equal("encounter.issue.unlink", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
    }

    [Fact]
    public async Task An_unlink_of_a_link_that_is_not_there_returns_false_and_writes_no_event_and_no_audit_row()
    {
        var removed = await UnlinkAsync();

        Assert.False(removed);
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task An_unlink_removes_only_that_encounters_link_and_leaves_the_others()
    {
        await LinkAsync(NewLink(number: 1001));
        await LinkAsync(NewLink(number: 1002));

        await UnlinkAsync(number: 1001);

        Assert.Equal(1L, await Count("issue_encounter"));
        Assert.Equal(1002L, await fixture.RootScalarAsync<long>("SELECT encounter FROM issue_encounter"));
    }

    [Fact]
    public async Task An_unlink_leaves_the_links_uuid_registry_row_because_the_service_may_not_delete_from_that_shared_table()
    {
        var link = NewLink();
        await LinkAsync(link);

        await UnlinkAsync();

        Assert.Equal(1L, await Count("uuid_registry", "table_name = 'issue_encounter'"));
    }

    [Fact]
    public async Task SEC_9_an_unlink_cannot_commit_without_its_audit_row()
    {
        await LinkAsync(NewLink());
        await using var db = fixture.CreateDbContext();
        var request = new UnlinkRequest(
            EncounterUuid, 1001, 42, Issue(),
            new IssueUnlinkedFromEncounterEvent(EncounterUuid, 1001, IssueUuid, "problem", EncounterTestData.PatientUuid, 42, Now.ToUniversalTime()),
            new AuditEntry(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.EncounterIssueUnlink, EncounterUuid, AuditOutcome.Success));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfEncounterIssueRegistry(db, new ThrowingAuditLog()).UnlinkAsync(request, CancellationToken.None));

        Assert.Equal(1L, await Count("issue_encounter"));   // the delete rolled back with the failed audit insert
    }
}

/// <summary>
/// The issue-link read adapters against rows the way <b>other legacy writers</b> leave them: other issue types, links between two patients' records, links whose issue legacy's REST
/// delete removed, issues with no uuid (LEG-4, DOM-9, ADR 0070).
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterIssueReaderTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Problem = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Allergy = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Medication = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OtherPatients = Guid.Parse("44444444-4444-4444-8444-444444444444");

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.RootExecuteAsync("DELETE FROM issue_encounter");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> InsertIssueAsync(Guid? uuid, string type, string title, long pid = 42)
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO lists (uuid, type, title, pid, activity) VALUES ({(uuid is null ? "NULL" : $"UNHEX('{uuid:N}')")}, '{type}', '{title}', {pid}, 1)");
        return await fixture.RootScalarAsync<long>("SELECT MAX(id) FROM lists");
    }

    private Task InsertLinkAsync(long listId, long number = 1001, long pid = 42, int resolved = 0, Guid? uuid = null) =>
        fixture.RootExecuteAsync(
            $"INSERT INTO issue_encounter (uuid, pid, list_id, encounter, resolved) VALUES ({(uuid is null ? "NULL" : $"UNHEX('{uuid:N}')")}, {pid}, {listId}, {number}, {resolved})");

    private async Task<EncounterIssuePage> ListAsync(long number = 1001, int? limit = null, int? offset = null)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfEncounterIssueReader(db).ListForEncounterAsync(number, 42, PageRequest.Create(limit, offset), CancellationToken.None);
    }

    private async Task<IssueReference?> DirectoryAsync(Guid uuid)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfIssueDirectory(db).FindAsync(uuid, CancellationToken.None);
    }

    [Fact]
    public async Task The_list_holds_the_problems_and_allergies_linked_to_the_encounter_with_their_kind_title_and_resolved_flag()
    {
        var problemId = await InsertIssueAsync(Problem, "medical_problem", "Asthma");
        var allergyId = await InsertIssueAsync(Allergy, "allergy", "Penicillin");
        var linkUuid = Guid.NewGuid();
        await InsertLinkAsync(problemId, resolved: 1, uuid: linkUuid);
        await InsertLinkAsync(allergyId);

        var page = await ListAsync();

        Assert.Equal(2, page.Total);
        var asthma = Assert.Single(page.Items, i => i.Kind == IssueKind.Problem);
        Assert.Equal(Problem, asthma.IssueUuid);
        Assert.Equal("Asthma", asthma.Title);
        Assert.True(asthma.Resolved);
        Assert.Equal(linkUuid, asthma.LinkUuid);
        Assert.NotNull(asthma.LinkedAt);
        var penicillin = Assert.Single(page.Items, i => i.Kind == IssueKind.Allergy);
        Assert.False(penicillin.Resolved);
        Assert.Null(penicillin.LinkUuid);   // a legacy row with no uuid is tolerated, not dropped
    }

    [Fact]
    public async Task ADR_0070_links_to_a_medication_a_surgery_and_any_other_type_are_not_listed()
    {
        await InsertLinkAsync(await InsertIssueAsync(Problem, "medical_problem", "Asthma"));
        await InsertLinkAsync(await InsertIssueAsync(Medication, "medication", "Aspirin"));
        await InsertLinkAsync(await InsertIssueAsync(Guid.NewGuid(), "surgery", "Appendectomy"));
        await InsertLinkAsync(await InsertIssueAsync(Guid.NewGuid(), "health_concern", "Housing"));

        var page = await ListAsync();

        Assert.Equal(1, page.Total);
        Assert.Equal("Asthma", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task A_link_between_two_different_patients_records_which_legacy_allows_is_never_shown()
    {
        var otherPatientsProblem = await InsertIssueAsync(OtherPatients, "medical_problem", "Someone elses", pid: 99);
        await InsertLinkAsync(otherPatientsProblem, pid: 42);   // legacy let a link name patient 42 and another patient's issue

        var page = await ListAsync();

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task A_link_whose_issue_was_deleted_by_legacys_rest_api_is_left_out_and_so_is_one_whose_issue_has_no_uuid()
    {
        await InsertLinkAsync(listId: 987654);   // the issue no longer exists
        await InsertLinkAsync(await InsertIssueAsync(null, "medical_problem", "No uuid yet"));
        await InsertLinkAsync(await InsertIssueAsync(Problem, "medical_problem", "Asthma"));

        var page = await ListAsync();

        Assert.Equal(1, page.Total);
        Assert.Equal("Asthma", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task Other_encounters_links_are_not_listed_and_paging_is_stable()
    {
        for (var i = 1; i <= 5; i++)
            await InsertLinkAsync(await InsertIssueAsync(Guid.NewGuid(), "medical_problem", $"P{i}"));
        await InsertLinkAsync(await InsertIssueAsync(Guid.NewGuid(), "medical_problem", "Other encounter"), number: 1002);

        var first = await ListAsync(limit: 2, offset: 0);
        var second = await ListAsync(limit: 2, offset: 2);
        var third = await ListAsync(limit: 2, offset: 4);

        Assert.Equal(["P1", "P2", "P3", "P4", "P5"], first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Title));
        Assert.All(new[] { first, second, third }, p => Assert.Equal(5, p.Total));
    }

    [Fact]
    public async Task An_encounter_with_no_links_has_an_empty_page_and_a_zero_total()
    {
        var page = await ListAsync(number: 5555);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task The_issue_directory_finds_a_problem_and_an_allergy_by_uuid_with_their_row_id_kind_and_patient()
    {
        var problemId = await InsertIssueAsync(Problem, "medical_problem", "Asthma");
        var allergyId = await InsertIssueAsync(Allergy, "allergy", "Penicillin");

        var problem = await DirectoryAsync(Problem);
        var allergy = await DirectoryAsync(Allergy);

        Assert.Equal(new IssueReference(Problem, problemId, IssueKind.Problem, 42, "Asthma"), problem);
        Assert.Equal(new IssueReference(Allergy, allergyId, IssueKind.Allergy, 42, "Penicillin"), allergy);
    }

    [Fact]
    public async Task ADR_0070_the_issue_directory_does_not_find_any_other_type_even_by_its_exact_uuid()
    {
        await InsertIssueAsync(Medication, "medication", "Aspirin");
        await InsertIssueAsync(Guid.Parse("55555555-5555-4555-8555-555555555555"), "surgery", "Appendectomy");
        await InsertIssueAsync(Guid.Parse("66666666-6666-4666-8666-666666666666"), "health_concern", "Housing");

        Assert.Null(await DirectoryAsync(Medication));
        Assert.Null(await DirectoryAsync(Guid.Parse("55555555-5555-4555-8555-555555555555")));
        Assert.Null(await DirectoryAsync(Guid.Parse("66666666-6666-4666-8666-666666666666")));
        Assert.Null(await DirectoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task The_issue_directory_does_not_find_an_issue_with_no_patient_key()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, activity) VALUES (UNHEX('{Problem:N}'), 'medical_problem', 'No pid', 1)");

        Assert.Null(await DirectoryAsync(Problem));
    }
}
