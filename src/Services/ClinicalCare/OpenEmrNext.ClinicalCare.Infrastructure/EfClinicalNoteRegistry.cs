using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IClinicalNoteRegistry"/> and the anti-corruption layer for a clinical note write (LEG-2, ADR 0074): one transaction holding what legacy's form writes (a new parent form in <c>forms</c> and
/// the note row under it), the <c>uuid_registry</c> row, the outbox row and the audit row.
/// </summary>
/// <remarks>
/// <para>
/// <b>The parent form id.</b> Legacy computes a new parent's <c>form_id</c> as <c>MAX(form_id) + 1</c> over the table, with no lock and no unique key, so two concurrent writers could take the same id and silently merge two
/// encounters' notes. This service serialises its own writers with a named database lock (<c>GET_LOCK</c>) around the read and the insert, on the transaction's own connection, and releases it afterwards. It cannot
/// serialise legacy's writer, which takes no lock: that race is inherent to the shared table and stated in the slice doc (L3). Each note gets its <b>own</b> parent form, so a note is never attached to a group someone else is editing.
/// </para>
/// <para>
/// Writes <c>user</c> only here (legacy sets it on insert and never changes it) and <c>groupname</c>, <c>authorized = 1</c>, <c>activity = 1</c>, and leaves <c>note_related_to</c> empty (legacy derives it by parsing
/// the text for issue references, which this service does not do; L2). Retry-safe by uuid (OPS-9).
/// </para>
/// </remarks>
public sealed class EfClinicalNoteRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IClinicalNoteRegistry
{
    public const string FormIdLock = "clinical_care.form_clinical_notes.form_id";
    private const int LockWaitSeconds = 10;

    public async Task RegisterAsync(ClinicalNote note, ClinicalNoteRecordedEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(note.Uuid);
        var f = note.Facts;
        var pid = note.Encounter.Patient.Pid;
        var number = note.Encounter.Number;
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var envelope = EventEnvelope.Serialize(
            ClinicalNoteRecordedEvent.EventType, @event.RecordedAtUtc, note.Uuid.ToString(), actor: audit.Actor.Subject, @event);

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            // The lock lives on one connection, and it must be taken BEFORE the transaction begins: under REPEATABLE READ the transaction's snapshot is fixed by its first read, so a writer that waited for the lock
            // inside its transaction would still read the table as it was before the winner committed, and take the same form id. So hold the connection open explicitly, lock, then begin.
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                var locked = await db.Database.SqlQuery<long?>($"SELECT GET_LOCK({FormIdLock}, {LockWaitSeconds}) AS Value").SingleAsync(ct);
                if (locked != 1)
                    throw new InvalidOperationException("Could not take the clinical note form-id lock; try again.");

                try
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);

                    var existing = await db.Database.SqlQuery<long>($"SELECT id AS Value FROM form_clinical_notes WHERE uuid = {uuidBytes}").ToListAsync(ct);
                    if (existing.Count > 0)
                        return existing[0];

                    var formId = await db.Database
                        .SqlQuery<long>($"SELECT COALESCE(MAX(form_id), 0) + 1 AS Value FROM form_clinical_notes")
                        .SingleAsync(ct);

                    var actor = await LegacyActor.ResolveAsync(db, note.ProviderId, ct);

                    // The note's own date is a calendar date; the forms row, like every forms row, carries when it was registered.
                    var registered = DateTime.Now;
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO forms (`date`, encounter, form_name, form_id, pid, `user`, groupname, authorized, deleted, formdir)
                        VALUES ({registered}, {number}, 'Clinical Notes Form', {formId}, {pid}, {actor.Username}, {actor.GroupName}, 1, 0, 'clinical_notes')
                        """, ct);

                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO form_clinical_notes
                            (form_id, uuid, `date`, pid, encounter, `user`, groupname, authorized, activity, code, codetext, description, clinical_notes_type, clinical_notes_category)
                        VALUES
                            ({formId}, {uuidBytes}, {f.Date}, {pid}, {numberText}, {actor.Username}, {actor.GroupName}, 1, 1, {f.Code}, {f.CodeText}, {f.Description}, {f.Type}, {f.Category})
                        """, ct);
                    var rowId = await db.Database.GetLastInsertIdAsync(ct);

                    await IssueRows.RegisterUuidAsync(db, uuidBytes, ct, "form_clinical_notes");

                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                        VALUES ({ClinicalNoteRecordedEvent.EventType}, {envelope}, {@event.RecordedAtUtc})
                        """, ct);

                    await auditLog.AppendAsync(audit, ct);   // last statement, same transaction (SEC-9)

                    await transaction.CommitAsync(ct);
                    return rowId;
                }
                finally
                {
                    // After the commit (or rollback): the next writer's snapshot is taken after this one is visible. Released whatever happened, because the connection goes back to a pool.
                    try
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync($"DO RELEASE_LOCK({FormIdLock})", CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // A lost connection has already dropped the lock with it, and this must not hide the failure that brought us here.
                    }
                }
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        });

        note.AssignGeneratedId(id);
    }
}
