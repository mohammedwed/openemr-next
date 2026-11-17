using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IVitalsUpdater"/> and the anti-corruption layer for a vitals edit (LEG-2, ADR 0068): one transaction holding a conditional <c>UPDATE</c> of the measurement
/// columns, the <c>VitalsCorrected</c> outbox row, and the audit row.
/// </summary>
/// <remarks>
/// <para>
/// The statement is conditional on the row's stamp (<c>last_updated = expected</c>), so a legacy edit made in the meantime is detected and never overwritten (LEG-10). It reaches only vitals
/// that <b>belong to this encounter and patient</b>, through the <c>forms</c> registry row, and not ones legacy has soft-deleted (CV-14): a vitals uuid from another encounter is not found, not
/// updated. It names only the measurement columns: who recorded it and when (<c>user</c>, <c>groupname</c>, <c>date</c>), <c>authorized</c>, <c>activity</c> and the <c>forms</c> row are not touched,
/// as legacy's own update does not touch them either. "Zero rows matched" means the stamp moved or the row is not here (MySqlConnector counts matched rows, not changed ones); a second read says
/// which.
/// </para>
/// <para>
/// An absent measurement is written as <c>0</c>, the column default legacy reads as "not recorded" (CV-12), and blood pressure as whole-number text (CV-8), exactly as a recording does.
/// As for encounters, a lost commit acknowledgement is reported as a conflict on retry, never a second write.
/// </para>
/// </remarks>
public sealed class EfVitalsUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IVitalsUpdater
{
    public async Task<UpdateOutcome> UpdateAsync(VitalsUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.VitalsUuid);
        var c = request.Change;
        var expected = request.Expected.Stamp;
        var number = request.EncounterNumber;
        var pid = request.PatientPid;
        var systolic = c.Systolic?.ToString(CultureInfo.InvariantCulture);
        var diastolic = c.Diastolic?.ToString(CultureInfo.InvariantCulture);
        var envelope = EventEnvelope.Serialize(
            VitalsCorrectedEvent.EventType, request.Event.CorrectedAtUtc, request.VitalsUuid.ToString(), actor: request.Audit.Actor.Subject, request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matched = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE form_vitals v
                SET v.weight = {c.WeightPounds ?? 0m}, v.height = {c.HeightInches ?? 0m}, v.temperature = {c.TemperatureFahrenheit ?? 0m},
                    v.temp_method = {c.TemperatureMethod}, v.bps = {systolic}, v.bpd = {diastolic}, v.pulse = {c.Pulse ?? 0},
                    v.respiration = {c.Respiration ?? 0}, v.oxygen_saturation = {c.OxygenSaturation ?? 0m}, v.waist_circ = {c.WaistInches ?? 0m},
                    v.head_circ = {c.HeadInches ?? 0m}, v.BMI = {c.Bmi ?? 0m}, v.BMI_status = {c.BmiStatus}, v.note = {c.Note}
                WHERE v.uuid = {uuidBytes} AND v.last_updated = {expected}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0
                              AND f.encounter = {number} AND f.pid = {pid})
                """, ct);

            if (matched == 0)
            {
                // Nothing was changed, so there is nothing to commit. Say whether the row moved or is not here.
                var current = await db.Database
                    .SqlQuery<DateTime>(
                        $"""
                        SELECT v.last_updated AS Value FROM form_vitals v
                        WHERE v.uuid = {uuidBytes}
                          AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0
                                      AND f.encounter = {number} AND f.pid = {pid})
                        """)
                    .ToListAsync(ct);

                return current.Count == 0
                    ? new UpdateOutcome(UpdateStatus.NotFound, null)
                    : new UpdateOutcome(UpdateStatus.Conflict, current[0]);
            }

            var stamp = (await db.Database
                .SqlQuery<DateTime>($"SELECT last_updated AS Value FROM form_vitals WHERE uuid = {uuidBytes}")
                .ToListAsync(ct)).Single();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({VitalsCorrectedEvent.EventType}, {envelope}, {request.Event.CorrectedAtUtc})
                """, ct);

            // Last statement, same transaction: the correction cannot commit without its audit row (SEC-9, ADR 0069 #3).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new UpdateOutcome(UpdateStatus.Updated, stamp);
        });
    }
}
