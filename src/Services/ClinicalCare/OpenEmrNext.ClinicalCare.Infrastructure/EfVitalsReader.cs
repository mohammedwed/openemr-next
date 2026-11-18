using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The read side of the vitals anti-corruption layer (LEG-2): turns legacy rows into <see cref="VitalsView"/>s,
/// tolerantly (DOM-9, LEG-4).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>form_vitals</c> has no encounter column, so a row belongs to an encounter through <c>forms</c>
///     (<c>form_id</c>, <c>formdir = 'vitals'</c>, the encounter number and pid), and only if that form is not deleted
///     (<c>getVitalsForPatientEncounter</c>). <c>EXISTS</c>, not a join, so a duplicated <c>forms</c> row cannot
///     duplicate a result.</item>
///   <item>A stored <c>0</c> is "not recorded" (CV-12); a value legacy stored as non-numeric text in <c>bps</c> or
///     <c>bpd</c> reads as absent rather than failing the page.</item>
///   <item>Newest first by date, then id, so pages are stable.</item>
/// </list>
/// </remarks>
public sealed class EfVitalsReader(ClinicalCareDbContext db) : IVitalsReader
{
    public async Task<VitalsView?> FindAsync(EncounterReference encounter, Guid vitalsUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(vitalsUuid);
        var number = encounter.Number;
        var pid = encounter.Patient.Pid;

        var rows = await db.Database
            .SqlQuery<VitalsRow>(
                $"""
                SELECT v.id AS Id, v.uuid AS Uuid, v.date AS Date, v.`user` AS RecordedBy, v.bps AS Bps, v.bpd AS Bpd,
                       v.weight AS Weight, v.height AS Height, v.temperature AS Temperature, v.temp_method AS TempMethod,
                       v.pulse AS Pulse, v.respiration AS Respiration, v.oxygen_saturation AS OxygenSaturation,
                       v.waist_circ AS Waist, v.head_circ AS Head, v.BMI AS Bmi, v.BMI_status AS BmiStatus, v.note AS Note,
                       v.last_updated AS LastUpdated
                FROM form_vitals v
                WHERE v.uuid = {uuidBytes}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0
                              AND f.encounter = {number} AND f.pid = {pid})
                """)
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<VitalsPage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct)
    {
        var number = encounter.Number;
        var pid = encounter.Patient.Pid;

        var rows = await db.Database
            .SqlQuery<VitalsRow>(
                $"""
                SELECT v.id AS Id, v.uuid AS Uuid, v.date AS Date, v.`user` AS RecordedBy, v.bps AS Bps, v.bpd AS Bpd,
                       v.weight AS Weight, v.height AS Height, v.temperature AS Temperature, v.temp_method AS TempMethod,
                       v.pulse AS Pulse, v.respiration AS Respiration, v.oxygen_saturation AS OxygenSaturation,
                       v.waist_circ AS Waist, v.head_circ AS Head, v.BMI AS Bmi, v.BMI_status AS BmiStatus, v.note AS Note,
                       v.last_updated AS LastUpdated
                FROM form_vitals v
                WHERE v.uuid IS NOT NULL
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0
                              AND f.encounter = {number} AND f.pid = {pid})
                ORDER BY v.date DESC, v.id DESC
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            // The order is stated again outside the raw SQL: EF wraps it in a subquery, and an inner ORDER BY is not
            // guaranteed to survive to the outer SELECT (same reasoning as EfEncounterReader).
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value
                FROM form_vitals v
                WHERE v.uuid IS NOT NULL
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0
                              AND f.encounter = {number} AND f.pid = {pid})
                """)
            .SingleAsync(ct);

        return new VitalsPage(rows.Select(ToView).ToList(), (int)total);
    }

    private const string PatientVitalsColumns =
        "v.id AS Id, v.uuid AS Uuid, v.date AS Date, v.`user` AS RecordedBy, v.bps AS Bps, v.bpd AS Bpd, " +
        "v.weight AS Weight, v.height AS Height, v.temperature AS Temperature, v.temp_method AS TempMethod, " +
        "v.pulse AS Pulse, v.respiration AS Respiration, v.oxygen_saturation AS OxygenSaturation, " +
        "v.waist_circ AS Waist, v.head_circ AS Head, v.BMI AS Bmi, v.BMI_status AS BmiStatus, v.note AS Note, " +
        "v.last_updated AS LastUpdated, fe.uuid AS EncounterUuid, pr.patient_uuid AS PatientUuid";

    // The encounter and the patient are reached the way every reader reaches them: through the forms row, the encounter's own row (not one legacy has deleted), and the patient reference table.
    private const string PatientVitalsFrom =
        " FROM form_vitals v" +
        " JOIN forms f ON f.form_id = v.id AND f.formdir = 'vitals' AND f.deleted = 0" +
        " JOIN form_encounter fe ON fe.encounter = f.encounter AND fe.pid = f.pid AND fe.uuid IS NOT NULL" +
        " JOIN clinical_care_patient_ref pr ON pr.pid = f.pid";

    private const string EncounterNotDeleted =
        " AND NOT EXISTS (SELECT 1 FROM forms nf WHERE nf.form_id = fe.id AND nf.formdir = 'newpatient' AND nf.deleted <> 0)";

    // These are fixed strings with numbered parameters ({0}, {1}); no value is ever concatenated into them.
    public async Task<PatientVitals?> FindAnywhereAsync(Guid vitalsUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(vitalsUuid);
        var rows = await db.Database
            .SqlQueryRaw<PatientVitalsRow>("SELECT " + PatientVitalsColumns + PatientVitalsFrom + " WHERE v.uuid = {0}" + EncounterNotDeleted + " LIMIT 1", uuidBytes)
            .ToListAsync(ct);

        return rows.Select(ToPatientVitals).FirstOrDefault();
    }

    public async Task<IReadOnlyList<PatientVitals>> ListForPatientAsync(PatientReference patient, int max, CancellationToken ct)
    {
        var pid = patient.Pid;
        var rows = await db.Database
            .SqlQueryRaw<PatientVitalsRow>(
                "SELECT " + PatientVitalsColumns + PatientVitalsFrom + " WHERE v.uuid IS NOT NULL AND f.pid = {0}" + EncounterNotDeleted + " ORDER BY v.date DESC, v.id DESC LIMIT {1}", pid, max)
            .ToListAsync(ct);

        // The order is stated again outside the raw SQL, as in ListForEncounterAsync.
        return rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).Select(ToPatientVitals).ToList();
    }

    private static PatientVitals ToPatientVitals(PatientVitalsRow row) =>
        new(ToView(row), Rfc4122GuidConverter.FromRfc4122Bytes(row.EncounterUuid), Rfc4122GuidConverter.FromRfc4122Bytes(row.PatientUuid));

    private static VitalsView ToView(VitalsRow row) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid!),
        row.Date,
        row.RecordedBy,
        Present(row.Weight),
        Present(row.Height),
        Present(row.Temperature),
        string.IsNullOrEmpty(row.TempMethod) ? null : row.TempMethod,
        WholeNumber(row.Bps),
        WholeNumber(row.Bpd),
        PresentWhole(row.Pulse),
        PresentWhole(row.Respiration),
        Present(row.OxygenSaturation),
        Present(row.Waist),
        Present(row.Head),
        Present(row.Bmi),
        string.IsNullOrEmpty(row.BmiStatus) ? null : row.BmiStatus,
        string.IsNullOrEmpty(row.Note) ? null : row.Note,
        row.LastUpdated);

    /// <summary>CV-12: a stored zero is "not recorded".</summary>
    private static decimal? Present(decimal? stored) => stored is null or 0m ? null : stored;

    private static int? PresentWhole(decimal? stored) =>
        stored is null or 0m ? null : (int)Math.Round(stored.Value, MidpointRounding.AwayFromZero);

    /// <summary>bps and bpd are free text in legacy; only a whole number is a reading we can return (DOM-9).</summary>
    private static int? WholeNumber(string? stored) =>
        int.TryParse(stored?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    private class VitalsRow
    {
        public long Id { get; set; }
        public byte[]? Uuid { get; set; }
        public DateTime? Date { get; set; }
        public string? RecordedBy { get; set; }
        public string? Bps { get; set; }
        public string? Bpd { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Height { get; set; }
        public decimal? Temperature { get; set; }
        public string? TempMethod { get; set; }
        public decimal? Pulse { get; set; }
        public decimal? Respiration { get; set; }
        public decimal? OxygenSaturation { get; set; }
        public decimal? Waist { get; set; }
        public decimal? Head { get; set; }
        public decimal? Bmi { get; set; }
        public string? BmiStatus { get; set; }
        public string? Note { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    private sealed class PatientVitalsRow : VitalsRow
    {
        public byte[] EncounterUuid { get; set; } = [];
        public byte[] PatientUuid { get; set; } = [];
    }
}
