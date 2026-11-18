using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IRecordDeleter"/> and the anti-corruption layer for a deletion (LEG-2, ADR 0076): one transaction that locks the record, checks that it is still the version the caller read, sets
/// legacy's own deletion flag, and writes the outbox row and the audit row. Nothing is ever removed: legacy's screens already hide a flagged record, and the row stays for legacy's own history.
/// </summary>
/// <remarks>
/// <para>The flag is legacy's, per kind: vitals and SOAP notes are deleted by <c>forms.deleted = 1</c> on the encounter's registry row (what legacy's <c>delete_form.php</c> does), a clinical note by its own
/// <c>activity = 0</c> (the flag legacy's note screens and this service's reader already filter on). The service holds <c>UPDATE</c> on the <c>forms.deleted</c> column and the note's <c>activity</c> column and on no other
/// way of changing a registry row, and it never sets either flag back.</para>
/// <para>The version is read under <c>FOR UPDATE</c> inside the transaction, so a legacy edit between the caller's read and this delete is detected and never silently deleted (LEG-10), and two deletions of one record
/// cannot both succeed. The record is reached only through this encounter's and patient's registry row, as every reader and updater does: a key from another encounter is not found, not deleted.</para>
/// </remarks>
public sealed class EfRecordDeleter(ClinicalCareDbContext db, IAuditLog auditLog) : IRecordDeleter
{
    public async Task<DeletionOutcome> DeleteAsync(RecordDeletionRequest request, CancellationToken ct)
    {
        var envelope = EventEnvelope.Serialize(
            request.Event.TypeName(), request.Event.DeletedAtUtc, request.Event.RecordId, actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var locked = request.Kind switch
            {
                DeletableRecord.Vitals => await LockVitalsAsync(request, ct),
                DeletableRecord.SoapNote => await LockSoapNoteAsync(request, ct),
                DeletableRecord.ClinicalNote => await LockClinicalNoteAsync(request, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };

            if (locked is null)
            {
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.NotFound, null);
            }

            if (!request.OfferedVersions.Contains(locked.Version, StringComparer.Ordinal))
            {
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.Conflict, locked.Version);
            }

            var changed = request.Kind == DeletableRecord.ClinicalNote
                ? await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE form_clinical_notes SET activity = 0 WHERE id = {locked.Id} AND pid = {request.PatientPid} AND activity = 1", ct)
                : await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE forms SET deleted = 1
                    WHERE form_id = {locked.Id} AND formdir = {FormDir(request.Kind)} AND encounter = {request.EncounterNumber} AND pid = {request.PatientPid} AND deleted = 0
                    """, ct);

            if (changed == 0)
            {
                // Locked a moment ago and gone now means the registry row is not what the reader saw (a duplicate or already-flagged one): nothing was deleted.
                await transaction.RollbackAsync(ct);
                return new DeletionOutcome(DeletionStatus.NotFound, null);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({request.Event.TypeName()}, {envelope}, {request.Event.DeletedAtUtc})
                """, ct);

            // Last statement, same transaction: the deletion cannot commit without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new DeletionOutcome(DeletionStatus.Deleted, null);
        });
    }

    private static string FormDir(DeletableRecord kind) => kind == DeletableRecord.Vitals ? "vitals" : "soap";

    private async Task<Locked?> LockVitalsAsync(RecordDeletionRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.Key, out var uuid))
            return null;

        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var (number, pid) = (request.EncounterNumber, request.PatientPid);
        var rows = await db.Database
            .SqlQuery<StampRow>(
                $"""
                SELECT v.id AS Id, v.last_updated AS Stamp FROM form_vitals v
                WHERE v.uuid = {bytes}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                FOR UPDATE
                """)
            .ToListAsync(ct);

        return rows.Count == 0 ? null : new Locked(rows[0].Id, ConcurrencyToken.From(rows[0].Stamp).ToETag());
    }

    private async Task<Locked?> LockClinicalNoteAsync(RecordDeletionRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.Key, out var uuid))
            return null;

        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var (number, pid) = (request.EncounterNumber, request.PatientPid);
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var rows = await db.Database
            .SqlQuery<StampRow>(
                $"""
                SELECT n.id AS Id, n.last_updated AS Stamp FROM form_clinical_notes n
                WHERE n.uuid = {bytes} AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = n.form_id AND f.formdir = 'clinical_notes' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                FOR UPDATE
                """)
            .ToListAsync(ct);

        return rows.Count == 0 ? null : new Locked(rows[0].Id, ConcurrencyToken.From(rows[0].Stamp).ToETag());
    }

    private async Task<Locked?> LockSoapNoteAsync(RecordDeletionRequest request, CancellationToken ct)
    {
        if (!long.TryParse(request.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var noteId))
            return null;

        var (number, pid) = (request.EncounterNumber, request.PatientPid);
        var locked = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT s.id AS Value FROM form_soap s
                WHERE s.id = {noteId}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = s.id AND f.formdir = 'soap' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                FOR UPDATE
                """)
            .ToListAsync(ct);

        // A SOAP note has no stamp, so its version is a hash of its four sections (ADR 0072), read in the same transaction as the lock.
        var current = locked.Count == 0 ? null : await EfSoapNoteReader.FindAsync(db, number, pid, noteId, ct);
        return current is null ? null : new Locked(noteId, current.Version().ToETag());
    }

    private sealed record Locked(long Id, string Version);

    private sealed class StampRow
    {
        public long Id { get; set; }
        public DateTime Stamp { get; set; }
    }
}
