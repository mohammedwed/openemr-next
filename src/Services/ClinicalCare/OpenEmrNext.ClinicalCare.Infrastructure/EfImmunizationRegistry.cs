using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IImmunizationRegistry"/> and the anti-corruption layer for an immunization write (LEG-2, ADR 0072): one transaction holding the row, its <c>uuid_registry</c> entry (so legacy
/// recognises the uuid as its own), the outbox row and the audit row. <c>update_date</c> is set because the column has no default (ADR 0072 #4). Retry-safe: a re-run finds the row by uuid and stops (OPS-9).
/// </summary>
public sealed class EfImmunizationRegistry(ClinicalCareDbContext db, IAuditLog auditLog) : IImmunizationRegistry
{
    public const string Table = "immunizations";

    public async Task RegisterAsync(Immunization immunization, ImmunizationRecordedEvent @event, AuditEntry audit, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(immunization.Uuid);
        var f = immunization.Facts;
        var envelope = EventEnvelope.Serialize(
            ImmunizationRecordedEvent.EventType, @event.RecordedAtUtc, immunization.Uuid.ToString(), actor: $"user:{immunization.ProviderId}", @event);

        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var existing = await db.Database.SqlQuery<long>($"SELECT id AS Value FROM immunizations WHERE uuid = {uuidBytes}").ToListAsync(ct);
            if (existing.Count > 0)
                return existing[0];

            // Blank text is stored as NULL, which is how the tolerant reader reads it back; flags and ids as legacy's columns expect.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO immunizations
                    (uuid, patient_id, administered_date, immunization_id, cvx_code, manufacturer, lot_number, administered_by_id, administered_by,
                     education_date, vis_date, note, create_date, update_date, created_by, updated_by, amount_administered, amount_administered_unit,
                     expiration_date, route, administration_site, added_erroneously, completion_status, information_source, refusal_reason,
                     ordering_provider, reason_code, reason_description, encounter_id)
                VALUES
                    ({uuidBytes}, {immunization.Patient.Pid}, {f.AdministeredDate}, {f.ImmunizationId}, {f.CvxCode}, {f.Manufacturer}, {f.LotNumber},
                     {f.AdministeredById}, {f.AdministeredBy}, {f.EducationDate}, {f.VisDate}, {f.Note}, {immunization.RecordedAt}, NOW(),
                     {immunization.ProviderId}, {immunization.ProviderId}, {f.AmountAdministered}, {f.AmountUnit}, {f.ExpirationDate}, {f.Route},
                     {f.AdministrationSite}, {f.EnteredInError}, {f.CompletionStatus}, {f.InformationSource}, {f.RefusalReason}, {f.OrderingProvider},
                     {f.ReasonCode}, {f.ReasonDescription}, {f.EncounterNumber})
                """, ct);
            var rowId = await db.Database.GetLastInsertIdAsync(ct);

            await IssueRows.RegisterUuidAsync(db, uuidBytes, ct, Table);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO clinical_care_outbox (event_type, payload, occurred_at)
                VALUES ({ImmunizationRecordedEvent.EventType}, {envelope}, {@event.RecordedAtUtc})
                """, ct);

            await auditLog.AppendAsync(audit, ct);   // last statement, same transaction (SEC-9)

            await transaction.CommitAsync(ct);
            return rowId;
        });

        immunization.AssignGeneratedId(id);
    }
}
