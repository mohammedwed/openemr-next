using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IVitalsRegistry"/> and the anti-corruption layer for legacy's vitals write (LEG-2):
/// one transaction that writes everything <c>VitalsService::saveVitalsArray</c> writes (OpenEMR 8.0.0), plus the outbox
/// row, so a set of vitals either exists completely or not at all (CV-10, CV-11, CV-13, EVT-1).
/// </summary>
/// <remarks>
/// Writes, in order, all parameterized (LEG-5): <c>form_vitals</c>; the <c>forms</c> row that links it to its encounter
/// (<c>form_vitals</c> has no encounter column; legacy does these two without a transaction); the <c>uuid_registry</c>
/// row; <c>clinical_care_outbox</c>. An absent measurement is written as <c>0</c>, the column default legacy reads as
/// "not recorded" (CV-12). Retry-safe by uuid, as <see cref="EfEncounterRegistry"/> is (OPS-9).
/// </remarks>
public sealed class EfVitalsRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IVitalsRegistry
{
    public async Task RegisterAsync(Vitals vitals, VitalsRecordedEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(vitals.Uuid);
        var envelope = EventEnvelope.Serialize(
            VitalsRecordedEvent.EventType, @event.RecordedAtUtc, vitals.Uuid.ToString(),
            actor: audit.Actor.Subject, @event);

        // bps and bpd are varchar(40) in legacy; a whole number as text is what its own REST validation requires (CV-8).
        var systolic = vitals.Systolic?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var diastolic = vitals.Diastolic?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var patientPid = vitals.Encounter.Patient.Pid;
        var number = vitals.Encounter.Number;

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Already committed by an earlier attempt whose acknowledgement was lost: nothing more to write.
            var existing = await db.Database
                .SqlQuery<long>($"SELECT id AS Value FROM form_vitals WHERE uuid = {uuidBytes}")
                .ToListAsync(ct);
            if (existing.Count > 0)
                return existing[0];

            // user and groupname are the provider's username and group, as the session would supply (CV-9, LegacyActor); an
            // unknown provider leaves the username NULL (F-CC-6). authorized is the 0/1 flag legacy's form controller writes
            // ($_SESSION['userauthorized']), not the provider id (F-CC-10).
            var actor = await LegacyActor.ResolveAsync(db, vitals.ProviderId, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO form_vitals
                    (uuid, date, pid, `user`, groupname, authorized, activity, bps, bpd, weight, height, temperature,
                     temp_method, pulse, respiration, note, BMI, BMI_status, waist_circ, head_circ, oxygen_saturation)
                VALUES ({uuidBytes}, {vitals.RecordedAt}, {patientPid}, {actor.Username}, {actor.GroupName},
                        1, 1, {systolic}, {diastolic}, {vitals.WeightPounds ?? 0m}, {vitals.HeightInches ?? 0m},
                        {vitals.TemperatureFahrenheit ?? 0m}, {vitals.TemperatureMethod}, {vitals.Pulse ?? 0},
                        {vitals.Respiration ?? 0}, {vitals.Note}, {vitals.Bmi ?? 0m}, {vitals.BmiStatus},
                        {vitals.WaistInches ?? 0m}, {vitals.HeadInches ?? 0m}, {vitals.OxygenSaturation ?? 0m})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO forms
                    (date, encounter, form_name, form_id, pid, `user`, groupname, authorized, deleted, formdir)
                VALUES ({vitals.RecordedAt}, {number}, 'Vitals', {rowId}, {patientPid}, {actor.Username}, {actor.GroupName},
                        1, 0, 'vitals')
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO uuid_registry (uuid, table_name, table_id, table_vertical, couchdb, document_drive, mapped, created)
                VALUES ({uuidBytes}, 'form_vitals', 'id', '', '', 0, 0, NOW())
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({VitalsRecordedEvent.EventType}, {envelope}, {@event.RecordedAtUtc})
                """, ct);

            // Last statement, same transaction: the vitals cannot exist without their audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(audit, ct);

            await transaction.CommitAsync(ct);
            return rowId;
        });

        vitals.AssignGeneratedId(id);
    }
}
