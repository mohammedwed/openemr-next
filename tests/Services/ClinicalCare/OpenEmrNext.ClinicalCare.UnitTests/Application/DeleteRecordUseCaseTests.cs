using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>
/// The deletion use case against in-memory fakes of every port (DOM-19, ADR 0076): orchestration only. What matters is the order of the answers (permission before anything is looked up), that a refusal writes
/// nothing but its own audit row, and that the right facts reach the adapter.
/// </summary>
public class DeleteRecordUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid RecordUuid = Guid.Parse("d0d0d0d0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeAccess(bool allowed) : IAccessControl
    {
        public List<(string? User, AccessRight Right)> Asked { get; } = [];

        public Task<bool> IsAllowedAsync(string? username, AccessRight right, CancellationToken ct)
        {
            Asked.Add((username, right));
            return Task.FromResult(allowed);
        }
    }

    private sealed class FakeEncounters(EncounterView? view) : IEncounterReader
    {
        public int Reads { get; private set; }

        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(view is not null && view.Uuid == encounterUuid ? view : null);
        }

        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeDeleter(DeletionOutcome outcome) : IRecordDeleter
    {
        public RecordDeletionRequest? Request { get; private set; }

        public Task<DeletionOutcome> DeleteAsync(RecordDeletionRequest request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(outcome);
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

    private static EncounterView Encounter(long? number = 1001, long? pid = 42) =>
        new(EncounterUuid, number, PatientUuid, pid, "AMB", null, null, null, 0, null, null, 7, null);

    private static RequestOrigin Origin(string? username = "dr_who") => new("trace-d", "192.0.2.40", new VerifiedActor(Guid.NewGuid(), 7, username));

    private static DeleteRecordCommand Command(DeletableRecord kind = DeletableRecord.Vitals, string? key = null, RequestOrigin? origin = null, Guid? encounter = null) =>
        new(kind, encounter ?? EncounterUuid, key ?? (kind == DeletableRecord.SoapNote ? "88" : RecordUuid.ToString()), ["\"v1\""], origin ?? Origin());

    private static (DeleteRecordUseCase UseCase, FakeAccess Access, FakeEncounters Encounters, FakeDeleter Deleter, FakeAuditLog Log) Build(
        bool allowed = true, EncounterView? encounter = null, DeletionOutcome? outcome = null, bool noEncounter = false)
    {
        var access = new FakeAccess(allowed);
        var encounters = new FakeEncounters(noEncounter ? null : encounter ?? Encounter());
        var deleter = new FakeDeleter(outcome ?? new DeletionOutcome(DeletionStatus.Deleted, null));
        var log = new FakeAuditLog();
        return (new DeleteRecordUseCase(access, encounters, deleter, log, new FixedClock(Now)), access, encounters, deleter, log);
    }

    [Fact]
    public async Task A_permitted_user_deletes_and_the_adapter_gets_the_facts_the_event_and_the_audit_row()
    {
        var (useCase, access, _, deleter, log) = Build();

        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(("dr_who", AccessRight.DeleteRecords), Assert.Single(access.Asked));
        var request = deleter.Request!;
        Assert.Equal((DeletableRecord.Vitals, RecordUuid.ToString(), 1001L, 42L), (request.Kind, request.Key, request.EncounterNumber, request.PatientPid));
        Assert.Equal(["\"v1\""], request.OfferedVersions);
        Assert.Equal("VitalsDeleted", request.Event.TypeName());
        Assert.Equal((EncounterUuid, PatientUuid, Now.UtcDateTime), (request.Event.EncounterUuid, request.Event.PatientUuid, request.Event.DeletedAtUtc));
        Assert.Equal((AuditActions.VitalsDelete, AuditOutcome.Success, RecordUuid), (request.Audit.Action, request.Audit.Outcome, request.Audit.Subject));
        Assert.Empty(log.Entries);   // the success row is committed by the adapter in its own transaction, not written here
    }

    [Theory]
    [InlineData(DeletableRecord.Vitals, "vitals.delete", "VitalsDeleted")]
    [InlineData(DeletableRecord.SoapNote, "soap.delete", "SoapNoteDeleted")]
    [InlineData(DeletableRecord.ClinicalNote, "clinical_note.delete", "ClinicalNoteDeleted")]
    public async Task Each_kind_has_its_own_audit_action_and_event_type(DeletableRecord kind, string action, string eventType)
    {
        var (useCase, _, _, deleter, _) = Build();

        await useCase.ExecuteAsync(Command(kind), CancellationToken.None);

        Assert.Equal((action, eventType), (deleter.Request!.Audit.Action, deleter.Request.Event.TypeName()));
    }

    [Fact]
    public async Task A_soap_note_is_named_by_its_form_id_in_the_audit_row_and_the_event()
    {
        var (useCase, _, _, deleter, _) = Build();

        await useCase.ExecuteAsync(Command(DeletableRecord.SoapNote), CancellationToken.None);

        Assert.Equal("soap:88", deleter.Request!.Audit.SubjectText);
        Assert.Equal("soap:88", deleter.Request.Event.RecordId);
    }

    [Fact]
    public async Task A_user_without_the_right_is_refused_before_the_encounter_or_record_is_looked_up_and_the_refusal_is_audited()
    {
        var (useCase, _, encounters, deleter, log) = Build(allowed: false);

        await Assert.ThrowsAsync<AccessDeniedException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(0, encounters.Reads);
        Assert.Null(deleter.Request);
        var row = Assert.Single(log.Entries);
        Assert.Equal((AuditActions.VitalsDelete, AuditOutcome.Failed), (row.Action, row.Outcome));
    }

    [Fact]
    public async Task A_refusal_for_permission_wins_over_a_missing_encounter()
    {
        var (useCase, _, _, _, _) = Build(allowed: false, noEncounter: true);

        await Assert.ThrowsAsync<AccessDeniedException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_session_with_no_username_is_asked_about_as_such_and_the_answer_is_the_ports(string? username)
    {
        // The use case does not decide what a nameless user may do: it asks, and the port (tested against legacy's rules) answers no.
        var (useCase, access, _, _, _) = Build(allowed: false);

        await Assert.ThrowsAsync<AccessDeniedException>(() => useCase.ExecuteAsync(Command(origin: Origin(username)), CancellationToken.None));

        Assert.Equal(username, Assert.Single(access.Asked).User);
    }

    [Fact]
    public async Task No_origin_at_all_is_refused_not_a_crash()
    {
        var (useCase, access, _, _, _) = Build(allowed: false);

        await Assert.ThrowsAsync<AccessDeniedException>(() => useCase.ExecuteAsync(new DeleteRecordCommand(DeletableRecord.Vitals, EncounterUuid, RecordUuid.ToString(), ["\"v\""], null), CancellationToken.None));

        Assert.Null(Assert.Single(access.Asked).User);
    }

    [Fact]
    public async Task An_unknown_encounter_is_not_found_and_audited()
    {
        var (useCase, _, _, deleter, log) = Build(noEncounter: true);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(deleter.Request);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Theory]
    [InlineData(null, 42L)]
    [InlineData(1001L, null)]
    public async Task An_encounter_with_no_number_or_no_known_patient_owns_nothing_so_the_record_is_not_found(long? number, long? pid)
    {
        var (useCase, _, _, deleter, log) = Build(encounter: Encounter(number, pid));

        await Assert.ThrowsAsync<RecordNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Null(deleter.Request);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task A_record_the_adapter_cannot_find_is_not_found_and_audited()
    {
        var (useCase, _, _, _, log) = Build(outcome: new DeletionOutcome(DeletionStatus.NotFound, null));

        await Assert.ThrowsAsync<RecordNotFoundException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }

    [Fact]
    public async Task A_changed_record_is_a_conflict_carrying_its_current_version_and_is_audited()
    {
        var (useCase, _, _, _, log) = Build(outcome: new DeletionOutcome(DeletionStatus.Conflict, "\"v2\""));

        var ex = await Assert.ThrowsAsync<RecordVersionConflictException>(() => useCase.ExecuteAsync(Command(), CancellationToken.None));

        Assert.Equal("\"v2\"", ex.CurrentVersion);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(log.Entries).Outcome);
    }
}
