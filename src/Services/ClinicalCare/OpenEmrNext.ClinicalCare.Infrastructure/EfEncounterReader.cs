using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The read side of the anti-corruption layer (LEG-2): turns whatever legacy rows hold into
/// <see cref="EncounterView"/>s, tolerantly (DOM-9, LEG-4). Most rows here were written by other legacy code, so a
/// column this service's own creation path always fills may be NULL, zero or empty.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>A row whose <c>uuid</c> is NULL cannot be addressed publicly and is left out; legacy backfills missing
///     uuids itself (<c>UuidRegistry::populateAllMissingUuids</c>).</item>
///   <item>A deleted encounter is one with any <c>newpatient</c> <c>forms</c> row flagged deleted. <c>NOT EXISTS</c>,
///     not a join: <c>forms</c> allows several such rows per encounter and a join would let a surviving row
///     outvote a deleted one (<c>EncounterService::getActiveEncounterByUuid</c>, CC-9).</item>
///   <item>The patient's uuid comes from Clinical Care's own <c>clinical_care_patient_ref</c> by <c>pid</c> (a unique key, so
///     the join cannot multiply rows). A row whose pid is not in that table reads with no patient uuid (DOM-9).</item>
///   <item>Newest first by <c>date</c>, not by encounter number: legacy found that an older visit entered late has a
///     higher number (<c>EncounterService::getMostRecentEncounterForPatient</c>). <c>id</c> breaks ties so pages are stable.</item>
/// </list>
/// </remarks>
public sealed class EfEncounterReader(ClinicalCareDbContext db) : IEncounterReader
{
    public async Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(encounterUuid);

        var rows = await db.Database
            .SqlQuery<EncounterRow>(
                $"""
                SELECT fe.id AS Id, fe.uuid AS Uuid, fe.encounter AS Number, fe.pid AS Pid, pd.patient_uuid AS PatientUuid,
                       fe.class_code AS ClassCode, fe.date AS Date, fe.onset_date AS OnsetDate, fe.reason AS Reason,
                       fe.facility_id AS FacilityId, fe.facility AS FacilityName, CAST(fe.pos_code AS SIGNED) AS PosCode,
                       fe.provider_id AS ProviderId, fe.sensitivity AS Sensitivity,
                       fe.last_update AS LastUpdate
                FROM form_encounter fe
                LEFT JOIN clinical_care_patient_ref pd ON pd.pid = fe.pid
                WHERE fe.uuid = {uuidBytes}
                  AND NOT EXISTS (SELECT 1 FROM forms f WHERE f.encounter = fe.encounter AND f.pid = fe.pid
                                  AND f.formdir = 'newpatient' AND f.deleted <> 0)
                """)
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
    {
        var pid = patient.Pid;

        var rows = await db.Database
            .SqlQuery<EncounterRow>(
                $"""
                SELECT fe.id AS Id, fe.uuid AS Uuid, fe.encounter AS Number, fe.pid AS Pid, pd.patient_uuid AS PatientUuid,
                       fe.class_code AS ClassCode, fe.date AS Date, fe.onset_date AS OnsetDate, fe.reason AS Reason,
                       fe.facility_id AS FacilityId, fe.facility AS FacilityName, CAST(fe.pos_code AS SIGNED) AS PosCode,
                       fe.provider_id AS ProviderId, fe.sensitivity AS Sensitivity,
                       fe.last_update AS LastUpdate
                FROM form_encounter fe
                LEFT JOIN clinical_care_patient_ref pd ON pd.pid = fe.pid
                WHERE fe.pid = {pid} AND fe.uuid IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM forms f WHERE f.encounter = fe.encounter AND f.pid = fe.pid
                                  AND f.formdir = 'newpatient' AND f.deleted <> 0)
                ORDER BY fe.date DESC, fe.id DESC
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            // EF wraps raw SQL in a subquery, and an inner ORDER BY is not guaranteed to survive to the outer
            // SELECT, so the order is stated again on the outside: pages must come back in a stable order.
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value
                FROM form_encounter fe
                WHERE fe.pid = {pid} AND fe.uuid IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM forms f WHERE f.encounter = fe.encounter AND f.pid = fe.pid
                                  AND f.formdir = 'newpatient' AND f.deleted <> 0)
                """)
            .SingleAsync(ct);

        return new EncounterPage(rows.Select(ToView).ToList(), (int)total);
    }

    private static EncounterView ToView(EncounterRow row) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid!),
        row.Number,
        row.PatientUuid is { Length: 16 } patientUuid ? Rfc4122GuidConverter.FromRfc4122Bytes(patientUuid) : null,
        row.Pid,
        row.ClassCode ?? "",
        row.Date,
        row.OnsetDate,
        row.Reason,
        row.FacilityId,
        row.FacilityName,
        row.PosCode is { } pos ? (int)pos : null,
        row.ProviderId ?? 0,
        row.Sensitivity,
        row.LastUpdate);

    // Every column is nullable or defaulted here on purpose: this is the shape legacy might hold, not the shape
    // this service writes. A zero-date datetime reads back as NULL through the connector's default handling.
    private sealed class EncounterRow
    {
        public long Id { get; set; }
        public byte[]? Uuid { get; set; }
        public long? Number { get; set; }
        public long? Pid { get; set; }
        public byte[]? PatientUuid { get; set; }
        public string? ClassCode { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? OnsetDate { get; set; }
        public string? Reason { get; set; }
        public int FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public long? PosCode { get; set; }
        public long? ProviderId { get; set; }
        public string? Sensitivity { get; set; }
        public DateTime? LastUpdate { get; set; }
    }
}
