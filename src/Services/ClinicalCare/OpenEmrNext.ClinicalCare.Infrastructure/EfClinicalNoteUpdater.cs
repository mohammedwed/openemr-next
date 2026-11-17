using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IClinicalNoteUpdater"/> and the anti-corruption layer for a clinical note edit (LEG-2, ADR 0068, 0074): one transaction holding a conditional <c>UPDATE</c> of the editable columns, the
/// <c>ClinicalNoteCorrected</c> outbox row and the audit row.
/// </summary>
/// <remarks>
/// The statement is conditional on the row's stamp (<c>last_updated = expected</c>, which the database moves for every writer, LEG-10), reaches only an <b>active</b> note that belongs to this encounter and patient through
/// both its own columns and its parent's <c>forms</c> row, and names only <c>date</c>, the derived <c>code</c> and <c>codetext</c>, <c>description</c>, type and category: not who wrote it (legacy sets it on insert only), group,
/// <c>authorized</c>, <c>activity</c> (which would be a delete), <c>form_id</c> or <c>note_related_to</c>. "Zero rows matched" means the stamp moved or the row is not here; a second read says which.
/// </remarks>
public sealed class EfClinicalNoteUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IClinicalNoteUpdater
{
    public async Task<UpdateOutcome> UpdateAsync(ClinicalNoteUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.NoteUuid);
        var f = request.Facts;
        var expected = request.Expected.Stamp;
        var number = request.EncounterNumber;
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var pid = request.PatientPid;
        var envelope = EventEnvelope.Serialize(
            ClinicalNoteCorrectedEvent.EventType, request.Event.CorrectedAtUtc, request.NoteUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matched = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE form_clinical_notes n
                SET n.`date` = {f.Date}, n.code = {f.Code}, n.codetext = {f.CodeText}, n.description = {f.Description},
                    n.clinical_notes_type = {f.Type}, n.clinical_notes_category = {f.Category}
                WHERE n.uuid = {uuidBytes} AND n.last_updated = {expected} AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                  AND EXISTS (SELECT 1 FROM forms fo WHERE fo.form_id = n.form_id AND fo.formdir = 'clinical_notes' AND fo.deleted = 0 AND fo.encounter = {number} AND fo.pid = {pid})
                """, ct);

            if (matched == 0)
            {
                var current = await db.Database
                    .SqlQuery<DateTime>(
                        $"""
                        SELECT n.last_updated AS Value FROM form_clinical_notes n
                        WHERE n.uuid = {uuidBytes} AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                          AND EXISTS (SELECT 1 FROM forms fo WHERE fo.form_id = n.form_id AND fo.formdir = 'clinical_notes' AND fo.deleted = 0 AND fo.encounter = {number} AND fo.pid = {pid})
                        """)
                    .ToListAsync(ct);

                return current.Count == 0 ? new UpdateOutcome(UpdateStatus.NotFound, null) : new UpdateOutcome(UpdateStatus.Conflict, current[0]);
            }

            var stamp = (await db.Database.SqlQuery<DateTime>($"SELECT last_updated AS Value FROM form_clinical_notes WHERE uuid = {uuidBytes}").ToListAsync(ct)).Single();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({ClinicalNoteCorrectedEvent.EventType}, {envelope}, {request.Event.CorrectedAtUtc})
                """, ct);
            await auditLog.AppendAsync(request.Audit, ct);   // same transaction: no correction without its audit row (SEC-9)

            await transaction.CommitAsync(ct);
            return new UpdateOutcome(UpdateStatus.Updated, stamp);
        });
    }
}
