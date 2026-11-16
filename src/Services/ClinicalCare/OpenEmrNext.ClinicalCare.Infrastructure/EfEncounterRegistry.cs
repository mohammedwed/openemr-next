using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterRegistry"/> and the anti-corruption layer for legacy's encounter
/// write (LEG-2): one transaction that writes everything legacy's own <c>insertEncounter</c> writes, plus the
/// outbox row, so the encounter either exists completely or not at all (CC-7, CC-11, EVT-1, ADR 0064).
/// </summary>
/// <remarks>
/// Writes, in order, all parameterized (LEG-5):
/// <list type="number">
///   <item><c>form_encounter</c>: the encounter row (<c>EncounterService.php:396-420</c>).</item>
///   <item><c>forms</c>: the <c>newpatient</c> registry row legacy's <c>addForm</c> writes next (CC-7). Legacy does
///     these two without a transaction (finding F-CC-4); here they are atomic.</item>
///   <item><c>uuid_registry</c>: the row <c>UuidRegistry::createUuid</c> inserts for every uuid it hands out
///     (CC-6, decision D-CC-1).</item>
///   <item><c>clinical_care_outbox</c>: the <c>EncounterCreated</c> event, wrapped in its envelope.</item>
/// </list>
/// Retry safety (OPS-9): the context retries transient failures, which re-runs the whole delegate. If a commit
/// succeeded but its acknowledgement was lost, the re-run must not insert a second copy, so the first step looks
/// the encounter up by its uuid and, if it is already there, stops. The model-visible id is assigned once, outside
/// the delegate, after success.
/// </remarks>
public sealed class EfEncounterRegistry(ClinicalCareDbContext db) : IEncounterRegistry
{
    public async Task RegisterAsync(Encounter encounter, EncounterCreatedEvent @event, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(encounter.Uuid);
        var envelope = EventEnvelope.Serialize(
            EncounterCreatedEvent.EventType, @event.CreatedAtUtc, encounter.Uuid.ToString(),
            actor: $"user:{encounter.ProviderId}", @event);

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Already committed by an earlier attempt whose acknowledgement was lost: nothing more to write.
            var existing = await db.Database
                .SqlQuery<long>($"SELECT id AS Value FROM form_encounter WHERE uuid = {uuidBytes}")
                .ToListAsync(ct);
            if (existing.Count > 0)
                return existing[0];

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO form_encounter
                    (uuid, date, onset_date, reason, facility, facility_id, pid, encounter, sensitivity,
                     pc_catid, provider_id, class_code, pos_code)
                VALUES
                    ({uuidBytes}, {encounter.Date}, {encounter.OnsetDate}, {encounter.Reason}, {encounter.FacilityName},
                     {encounter.FacilityId}, {encounter.Patient.Pid}, {encounter.Number}, {encounter.Sensitivity},
                     {encounter.CalendarCategoryId}, {encounter.ProviderId}, {encounter.ClassCode}, {encounter.PosCode})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            // Legacy's addForm: who did it comes from the session (username and group). Here the provider id is
            // all there is, so both are looked up from it. A provider that does not exist leaves them NULL; that
            // is a tracked finding (F-CC-6), not something this adapter invents a value for. The group is the
            // user's `groups` entry, falling back to the literal 'Default' legacy itself writes in one path
            // (CdaTemplateImportDispose.php:791); not verified against a signed-in session (slice doc section 8).
            //
            // `authorized` is written as 1, not the provider id legacy's insertEncounter passes. The column is a
            // tinyint(4) and legacy runs with sql_mode = '' so it silently truncates an id above 127 to 127; this
            // service connects in strict mode, where that is a hard error (found by the integration tests, F-CC-10).
            // Every other legacy caller passes the 0/1 "authorized" flag, and the only meaning anything gives the
            // column is non-zero versus zero, so 1 is the same for every real provider. A deliberate deviation (CC-7).
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO forms
                    (date, encounter, form_name, form_id, pid, user, groupname, authorized, deleted, formdir)
                SELECT {encounter.Date}, {encounter.Number}, 'New Patient Encounter', {rowId}, {encounter.Patient.Pid},
                       u.username,
                       COALESCE((SELECT g.name FROM `groups` g WHERE g.user = u.username LIMIT 1), 'Default'),
                       1, 0, 'newpatient'
                FROM (SELECT {encounter.ProviderId} AS id) p
                LEFT JOIN users u ON u.id = p.id
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO uuid_registry (uuid, table_name, table_id, table_vertical, couchdb, document_drive, mapped, created)
                VALUES ({uuidBytes}, 'form_encounter', 'id', '', '', 0, 0, NOW())
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({EncounterCreatedEvent.EventType}, {envelope}, {@event.CreatedAtUtc})
                """, ct);

            await transaction.CommitAsync(ct);
            return rowId;
        });

        encounter.AssignGeneratedId(id);
    }
}
