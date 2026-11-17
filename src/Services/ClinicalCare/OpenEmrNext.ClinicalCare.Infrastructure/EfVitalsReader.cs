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

    private sealed class VitalsRow
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
}
