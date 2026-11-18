using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IProblemReader"/>: reads <c>lists</c> rows of <c>type = 'medical_problem'</c> only (ADR 0070 #2), written by any legacy
/// route, tolerantly (DOM-9, LEG-4). A row with no <c>uuid</c> is not addressable, so it is not returned (ADR 0070 #6, the encounter precedent CC-16).
/// </summary>
/// <remarks>
/// The patient's uuid comes from this service's own patient reference (ADR 0066), joined on <c>pid</c>; a row whose <c>pid</c> matches no known patient is
/// still read, with no patient uuid. A zero-date <c>datetime</c> reads back as <c>NULL</c> through the connector's default handling.
/// </remarks>
public sealed class EfProblemReader(ClinicalCareDbContext db) : IProblemReader
{
    public async Task<ProblemView?> FindAsync(Guid problemUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(problemUuid);

        var rows = await db.Database
            .SqlQuery<ProblemRow>(
                $"""
                SELECT l.uuid AS Uuid, pr.patient_uuid AS PatientUuid, l.pid AS Pid, l.title AS Title, l.begdate AS BeginDate,
                       l.enddate AS EndDate, l.diagnosis AS Diagnosis, l.outcome AS Outcome, l.occurrence AS Occurrence,
                       l.referredby AS ReferredBy, l.comments AS Comments, l.`date` AS RecordedAt, l.`user` AS RecordedBy,
                       l.modifydate AS LastModified, l.verification AS Verification
                FROM lists l
                LEFT JOIN clinical_care_patient_ref pr ON pr.pid = l.pid
                WHERE l.uuid = {uuidBytes} AND l.type = {EfProblemRegistry.ProblemType}
                """)
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<ProblemPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
    {
        var pid = patient.Pid;
        var (from, until) = (page.Updated?.From, page.Updated?.Until);

        // Newest first by when the problem began, then by row id so the order is total and pages never overlap or skip.
        var rows = await db.Database
            .SqlQuery<ProblemRow>(
                $"""
                SELECT l.uuid AS Uuid, pr.patient_uuid AS PatientUuid, l.pid AS Pid, l.title AS Title, l.begdate AS BeginDate,
                       l.enddate AS EndDate, l.diagnosis AS Diagnosis, l.outcome AS Outcome, l.occurrence AS Occurrence,
                       l.referredby AS ReferredBy, l.comments AS Comments, l.`date` AS RecordedAt, l.`user` AS RecordedBy,
                       l.modifydate AS LastModified, l.verification AS Verification
                FROM lists l
                LEFT JOIN clinical_care_patient_ref pr ON pr.pid = l.pid
                WHERE l.pid = {pid} AND l.type = {EfProblemRegistry.ProblemType} AND l.uuid IS NOT NULL
                  AND ({from} IS NULL OR l.modifydate >= {from}) AND ({until} IS NULL OR l.modifydate < {until})
                ORDER BY l.begdate DESC, l.id DESC
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value FROM lists l
                WHERE l.pid = {pid} AND l.type = {EfProblemRegistry.ProblemType} AND l.uuid IS NOT NULL
                  AND ({from} IS NULL OR l.modifydate >= {from}) AND ({until} IS NULL OR l.modifydate < {until})
                """)
            .SingleAsync(ct);

        return new ProblemPage(rows.Select(ToView).ToList(), (int)total);
    }

    private static ProblemView ToView(ProblemRow row) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(row.Uuid!),
        row.PatientUuid is { Length: 16 } patientUuid ? Rfc4122GuidConverter.FromRfc4122Bytes(patientUuid) : null,
        row.Pid,
        row.Title,
        row.BeginDate,
        row.EndDate,
        string.IsNullOrWhiteSpace(row.Diagnosis) ? null : row.Diagnosis,
        row.Outcome,
        row.Occurrence,
        string.IsNullOrEmpty(row.ReferredBy) ? null : row.ReferredBy,
        string.IsNullOrEmpty(row.Comments) ? null : row.Comments,
        row.RecordedAt,
        string.IsNullOrEmpty(row.RecordedBy) ? null : row.RecordedBy,
        row.LastModified,
        string.IsNullOrWhiteSpace(row.Verification) ? null : row.Verification);

    // Every column is nullable or defaulted here on purpose: this is the shape legacy might hold, not the shape this service writes.
    private sealed class ProblemRow
    {
        public byte[]? Uuid { get; set; }
        public byte[]? PatientUuid { get; set; }
        public long? Pid { get; set; }
        public string? Title { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Diagnosis { get; set; }
        public int Outcome { get; set; }
        public int Occurrence { get; set; }
        public string? ReferredBy { get; set; }
        public string? Comments { get; set; }
        public DateTime? RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public DateTime? LastModified { get; set; }
        public string? Verification { get; set; }
    }
}
