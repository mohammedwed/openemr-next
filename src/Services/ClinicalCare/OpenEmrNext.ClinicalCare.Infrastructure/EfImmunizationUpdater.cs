using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IImmunizationUpdater"/> (ADR 0072). The table has no usable stamp, so the version is a hash of the owned columns and is verified <b>under a row lock</b>: lock the row, read it with the
/// same mapping the reader uses, compare its version with the one the caller read, and only then write. A legacy edit landing between the caller's read and this lock is therefore still caught.
/// The write touches only the owned columns and <c>update_date</c>; who recorded the row, when, and the patient are left alone.
/// </summary>
public sealed class EfImmunizationUpdater(ClinicalCareDbContext db, IAuditLog auditLog) : IImmunizationUpdater
{
    public async Task<ContentUpdateOutcome> UpdateAsync(ImmunizationUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.ImmunizationUuid);
        var f = request.Facts;
        var pid = request.PatientPid;
        var envelope = EventEnvelope.Serialize(
            ImmunizationCorrectedEvent.EventType, request.Event.CorrectedAtUtc, request.ImmunizationUuid.ToString(), actor: "unknown", request.Event);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // The lock first, scoped to the patient in the request so another patient's row is simply not found; then the read, under the lock.
            var locked = await db.Database
                .SqlQuery<long>($"SELECT id AS Value FROM immunizations WHERE uuid = {uuidBytes} AND patient_id = {pid} FOR UPDATE")
                .ToListAsync(ct);
            if (locked.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return new ContentUpdateOutcome(UpdateStatus.NotFound, null);
            }

            var current = await EfImmunizationReader.FindAsync(db, request.ImmunizationUuid, ct);
            if (current is null)
            {
                await transaction.RollbackAsync(ct);
                return new ContentUpdateOutcome(UpdateStatus.NotFound, null);
            }

            var currentVersion = current.Version();
            if (currentVersion != request.Expected)
            {
                await transaction.RollbackAsync(ct);
                return new ContentUpdateOutcome(UpdateStatus.Conflict, currentVersion);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE immunizations
                SET administered_date = {f.AdministeredDate}, immunization_id = {f.ImmunizationId}, cvx_code = {f.CvxCode}, manufacturer = {f.Manufacturer},
                    lot_number = {f.LotNumber}, administered_by_id = {f.AdministeredById}, administered_by = {f.AdministeredBy},
                    education_date = {f.EducationDate}, vis_date = {f.VisDate}, note = {f.Note}, amount_administered = {f.AmountAdministered},
                    amount_administered_unit = {f.AmountUnit}, expiration_date = {f.ExpirationDate}, route = {f.Route},
                    administration_site = {f.AdministrationSite}, added_erroneously = {f.EnteredInError}, completion_status = {f.CompletionStatus},
                    information_source = {f.InformationSource}, refusal_reason = {f.RefusalReason}, ordering_provider = {f.OrderingProvider},
                    reason_code = {f.ReasonCode}, reason_description = {f.ReasonDescription}, encounter_id = {f.EncounterNumber}, update_date = NOW()
                WHERE uuid = {uuidBytes} AND patient_id = {pid}
                """, ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({ImmunizationCorrectedEvent.EventType}, {envelope}, {request.Event.CorrectedAtUtc})
                """, ct);
            await auditLog.AppendAsync(request.Audit, ct);   // same transaction: no correction without its audit row (SEC-9)

            await transaction.CommitAsync(ct);

            var after = await EfImmunizationReader.FindAsync(db, request.ImmunizationUuid, ct);
            return new ContentUpdateOutcome(UpdateStatus.Updated, after?.Version());
        });
    }
}
