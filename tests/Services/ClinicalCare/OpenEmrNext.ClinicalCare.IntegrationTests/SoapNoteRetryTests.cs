using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The SOAP registry's retry guard (ADR 0077, closing L2 of the SOAP slice): a transaction the execution strategy runs again after a lost commit acknowledgement must not write a second note. The fault is real: the
/// first run commits, and an interceptor then throws as the driver would if the reply never arrived.
/// </summary>
[Collection("ClinicalCareContainer")]
public class SoapNoteRetryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly EncounterReference Encounter = EncounterReference.Create(Guid.NewGuid(), 1001, EncounterTestData.PatientUuid, 42);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class CommitAckLost : Exception;

    /// <summary>Throws after the first <c>n</c> commits have gone through: the data is there, the caller is told it failed.</summary>
    private sealed class LoseCommitAcks(int n) : DbTransactionInterceptor
    {
        private int _remaining = n;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            Interlocked.Decrement(ref _remaining) >= 0 ? throw new CommitAckLost() : Task.CompletedTask;
    }

    /// <summary>Retries on the fault above and nothing else, with no delay.</summary>
    private sealed class RetryOnLostAck(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 3, TimeSpan.FromMilliseconds(1))
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is CommitAckLost;
    }

    private ClinicalCareDbContext ContextThatLoses(int acks) =>
        new(new DbContextOptionsBuilder<ClinicalCareDbContext>()
            .UseMySql(fixture.ProductionConnectionString, new MariaDbServerVersion(new Version(12, 3, 3)), o => o.ExecutionStrategy(d => new RetryOnLostAck(d)))
            .AddInterceptors(new LoseCommitAcks(acks))
            .Options);

    private static SoapNote NewNote() =>
        SoapNote.Record(Encounter, SoapSections.Create(new SoapInput("cough for 3 days", "lungs clear", "viral URI", "rest and fluids")), 7, Now);

    private static Task RegisterAsync(ClinicalCareDbContext db, SoapNote note) =>
        new EfSoapNoteRegistry(db, new EfAuditLog(db)).RegisterAsync(
            note, new SoapNoteRecordedEvent(0, note.Encounter.Uuid, note.Encounter.Number, note.Encounter.Patient.Uuid, note.Encounter.Patient.Pid, ["subjective"], Now.ToUniversalTime()),
            id => new AuditEntry(Now.ToUniversalTime(), new AuditActor("user:7", 7, false), AuditActions.SoapRecord, $"soap:{id}", AuditOutcome.Success, changedFields: ["subjective"]),
            CancellationToken.None);

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    [Fact]
    public async Task A_commit_whose_acknowledgement_is_lost_and_is_then_retried_writes_one_note()
    {
        await using var db = ContextThatLoses(1);
        var note = NewNote();

        await RegisterAsync(db, note);

        Assert.Equal(1L, await Count("form_soap"));
        Assert.Equal(1L, await Count("forms", "formdir = 'soap'"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'SoapNoteRecorded'"));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'soap.record'"));
        // The caller is given the note that was written, not an id of zero.
        Assert.Equal(await fixture.RootScalarAsync<long>("SELECT id FROM form_soap"), note.Id);
    }

    [Fact]
    public async Task Several_lost_acknowledgements_in_a_row_still_write_one_note()
    {
        await using var db = ContextThatLoses(2);

        await RegisterAsync(db, NewNote());

        Assert.Equal(1L, await Count("form_soap"));
        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'SoapNoteRecorded'"));
    }

    [Fact]
    public async Task The_event_carries_the_attempt_it_belongs_to_and_the_note_it_wrote()
    {
        await using var db = fixture.CreateDbContext();
        var note = NewNote();

        await RegisterAsync(db, note);

        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox WHERE event_type = 'SoapNoteRecorded'");
        using var doc = System.Text.Json.JsonDocument.Parse(payload!);
        var body = doc.RootElement.GetProperty("payload");
        Assert.Equal(note.Id, body.GetProperty("noteId").GetInt64());
        Assert.NotEqual(Guid.Empty, body.GetProperty("attemptId").GetGuid());
    }

    [Fact]
    public async Task Two_separate_notes_are_two_notes_each_with_its_own_attempt()
    {
        await using var db = ContextThatLoses(2);   // the guard must not mistake a second request for a retry of the first
        await RegisterAsync(db, NewNote());
        await using var other = fixture.CreateDbContext();
        await RegisterAsync(other, NewNote());

        Assert.Equal(2L, await Count("form_soap"));
        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(DISTINCT JSON_VALUE(payload, '$.payload.attemptId')) FROM clinical_care_outbox WHERE event_type = 'SoapNoteRecorded'"));
    }
}
