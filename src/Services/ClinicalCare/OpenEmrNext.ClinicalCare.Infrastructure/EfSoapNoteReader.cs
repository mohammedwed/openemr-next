using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The read side of the SOAP anti-corruption layer (LEG-2, ADR 0073): turns legacy rows into <see cref="SoapNoteView"/>s, tolerantly (DOM-9). <c>form_soap</c> has no encounter column, so a note belongs to an encounter
/// through its <c>forms</c> row (<c>formdir = 'soap'</c>, the encounter number and pid, not deleted). <c>EXISTS</c>, not a join, so a duplicated <c>forms</c> row cannot duplicate a result. The query text is a
/// constant shared with the updater, which re-reads inside its lock so the version it checks is built by the same mapping a caller's was.
/// </summary>
public sealed class EfSoapNoteReader(ClinicalCareDbContext db) : ISoapNoteReader
{
    private const string Select = """
        SELECT s.id AS Id, s.`date` AS Date, s.`user` AS RecordedBy, s.subjective AS Subjective, s.objective AS Objective, s.assessment AS Assessment, s.plan AS Plan
        FROM form_soap s
        WHERE EXISTS (SELECT 1 FROM forms f WHERE f.form_id = s.id AND f.formdir = 'soap' AND f.deleted = 0 AND f.encounter = {0} AND f.pid = {1})
        """;

    public async Task<SoapNoteView?> FindAsync(EncounterReference encounter, long noteId, CancellationToken ct) =>
        await FindAsync(db, encounter.Number, encounter.Patient.Pid, noteId, ct);

    internal static async Task<SoapNoteView?> FindAsync(ClinicalCareDbContext db, long encounterNumber, long pid, long noteId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<SoapRow>(Select + " AND s.id = {2}", encounterNumber, pid, noteId).ToListAsync(ct);
        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<SoapNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct)
    {
        var number = encounter.Number;
        var pid = encounter.Patient.Pid;

        var rows = await db.Database
            .SqlQueryRaw<SoapRow>(Select + " ORDER BY s.`date` DESC, s.id DESC LIMIT {2} OFFSET {3}", number, pid, page.Limit, page.Offset)
            // The order is stated again outside the raw SQL: EF wraps it in a subquery, which does not guarantee an inner ORDER BY survives (as EfVitalsReader).
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value FROM form_soap s
                WHERE EXISTS (SELECT 1 FROM forms f WHERE f.form_id = s.id AND f.formdir = 'soap' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                """)
            .SingleAsync(ct);

        return new SoapNotePage(rows.Select(ToView).ToList(), (int)total);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static SoapNoteView ToView(SoapRow r) => new(r.Id, r.Date, Blank(r.RecordedBy), Blank(r.Subjective), Blank(r.Objective), Blank(r.Assessment), Blank(r.Plan));

    private sealed class SoapRow
    {
        public long Id { get; set; }
        public DateTime? Date { get; set; }
        public string? RecordedBy { get; set; }
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
    }
}
