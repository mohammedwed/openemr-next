using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAllergyReader"/>: reads <c>lists</c> rows of <c>type = 'allergy'</c> only (ADR 0070 #2), written by any legacy route,
/// tolerantly (DOM-9, LEG-4). A row with no <c>uuid</c> is not addressable, so it is not returned (ADR 0070 #6). Legacy stores an absent <c>reaction</c> or
/// <c>verification</c> as an empty string (NOT NULL columns), which reads back as <c>null</c>.
/// </summary>
public sealed class EfAllergyReader(ClinicalCareDbContext db) : IAllergyReader
{
    public async Task<AllergyView?> FindAsync(Guid allergyUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(allergyUuid);

        var rows = await db.Database
            .SqlQuery<AllergyRow>(
                $"""
                SELECT l.uuid AS Uuid, pr.patient_uuid AS PatientUuid, l.pid AS Pid, l.title AS Title, l.begdate AS BeginDate,
                       l.enddate AS EndDate, l.diagnosis AS Diagnosis, l.reaction AS Reaction, l.severity_al AS Severity,
                       l.verification AS Verification, l.comments AS Comments, l.`date` AS RecordedAt, l.`user` AS RecordedBy,
                       l.modifydate AS LastModified
                FROM lists l
                LEFT JOIN clinical_care_patient_ref pr ON pr.pid = l.pid
                WHERE l.uuid = {uuidBytes} AND l.type = {EfAllergyRegistry.AllergyType}
                """)
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<AllergyPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
    {
        var pid = patient.Pid;
        var (from, until) = (page.Updated?.From, page.Updated?.Until);

        // Newest first by when the allergy began, then by row id so the order is total and pages never overlap or skip.
        var rows = await db.Database
            .SqlQuery<AllergyRow>(
                $"""
                SELECT l.uuid AS Uuid, pr.patient_uuid AS PatientUuid, l.pid AS Pid, l.title AS Title, l.begdate AS BeginDate,
                       l.enddate AS EndDate, l.diagnosis AS Diagnosis, l.reaction AS Reaction, l.severity_al AS Severity,
                       l.verification AS Verification, l.comments AS Comments, l.`date` AS RecordedAt, l.`user` AS RecordedBy,
                       l.modifydate AS LastModified
                FROM lists l
                LEFT JOIN clinical_care_patient_ref pr ON pr.pid = l.pid
                WHERE l.pid = {pid} AND l.type = {EfAllergyRegistry.AllergyType} AND l.uuid IS NOT NULL
                  AND ({from} IS NULL OR l.modifydate >= {from}) AND ({until} IS NULL OR l.modifydate < {until})
                ORDER BY l.begdate DESC, l.id DESC
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value FROM lists l
                WHERE l.pid = {pid} AND l.type = {EfAllergyRegistry.AllergyType} AND l.uuid IS NOT NULL
                  AND ({from} IS NULL OR l.modifydate >= {from}) AND ({until} IS NULL OR l.modifydate < {until})
                """)
            .SingleAsync(ct);

        return new AllergyPage(rows.Select(ToView).ToList(), (int)total);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AllergyView ToView(AllergyRow row) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid!),
        row.PatientUuid is { Length: 16 } patientUuid ? Rfc4122GuidConverter.FromRfc4122Bytes(patientUuid) : null,
        row.Pid,
        row.Title,
        row.BeginDate,
        row.EndDate,
        NullIfBlank(row.Diagnosis),
        NullIfBlank(row.Reaction),
        NullIfBlank(row.Severity),
        NullIfBlank(row.Verification),
        string.IsNullOrEmpty(row.Comments) ? null : row.Comments,
        row.RecordedAt,
        string.IsNullOrEmpty(row.RecordedBy) ? null : row.RecordedBy,
        row.LastModified);

    // Every column is nullable or defaulted here on purpose: this is the shape legacy might hold, not the shape this service writes.
    private sealed class AllergyRow
    {
        public byte[]? Uuid { get; set; }
        public byte[]? PatientUuid { get; set; }
        public long? Pid { get; set; }
        public string? Title { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Diagnosis { get; set; }
        public string? Reaction { get; set; }
        public string? Severity { get; set; }
        public string? Verification { get; set; }
        public string? Comments { get; set; }
        public DateTime? RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public DateTime? LastModified { get; set; }
    }
}
