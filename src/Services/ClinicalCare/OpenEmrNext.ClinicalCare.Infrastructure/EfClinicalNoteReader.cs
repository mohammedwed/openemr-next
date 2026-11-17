using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The read side of the clinical note anti-corruption layer (LEG-2, ADR 0074): turns legacy rows into <see cref="ClinicalNoteView"/>s, tolerantly (DOM-9). A note belongs to an encounter two ways at once, and
/// both must agree: its own <c>encounter</c> (text) and <c>pid</c> columns, and the <c>forms</c> row of its parent form (<c>formdir = 'clinical_notes'</c>, not deleted). Only <c>activity = 1</c> notes are read: legacy's
/// own form retires a note by setting it to 0. A row with no <c>uuid</c> is not addressable (ADR 0070 #6). <c>EXISTS</c>, not a join, because a parent form groups several notes and may be duplicated.
/// </summary>
public sealed class EfClinicalNoteReader(ClinicalCareDbContext db) : IClinicalNoteReader
{
    public async Task<ClinicalNoteView?> FindAsync(EncounterReference encounter, Guid noteUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(noteUuid);
        var number = encounter.Number;
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var pid = encounter.Patient.Pid;

        var rows = await db.Database
            .SqlQuery<NoteRow>(
                $"""
                SELECT n.uuid AS Uuid, n.`date` AS Date, n.clinical_notes_type AS Type, n.clinical_notes_category AS Category, n.description AS Description,
                       n.code AS Code, n.codetext AS CodeText, n.`user` AS RecordedBy, n.last_updated AS LastUpdated, n.id AS Id
                FROM form_clinical_notes n
                WHERE n.uuid = {uuidBytes} AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = n.form_id AND f.formdir = 'clinical_notes' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                """)
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<ClinicalNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct)
    {
        var number = encounter.Number;
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        var pid = encounter.Patient.Pid;

        var rows = await db.Database
            .SqlQuery<NoteRow>(
                $"""
                SELECT n.uuid AS Uuid, n.`date` AS Date, n.clinical_notes_type AS Type, n.clinical_notes_category AS Category, n.description AS Description,
                       n.code AS Code, n.codetext AS CodeText, n.`user` AS RecordedBy, n.last_updated AS LastUpdated, n.id AS Id
                FROM form_clinical_notes n
                WHERE n.uuid IS NOT NULL AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = n.form_id AND f.formdir = 'clinical_notes' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                ORDER BY n.`date` DESC, n.id DESC
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            // The order is stated again outside the raw SQL: EF wraps it in a subquery, which does not guarantee an inner ORDER BY survives (as EfVitalsReader).
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value FROM form_clinical_notes n
                WHERE n.uuid IS NOT NULL AND n.activity = 1 AND n.pid = {pid} AND n.encounter = {numberText}
                  AND EXISTS (SELECT 1 FROM forms f WHERE f.form_id = n.form_id AND f.formdir = 'clinical_notes' AND f.deleted = 0 AND f.encounter = {number} AND f.pid = {pid})
                """)
            .SingleAsync(ct);

        return new ClinicalNotePage(rows.Select(ToView).ToList(), (int)total);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ClinicalNoteView ToView(NoteRow r) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid!), r.Date, Blank(r.Type), Blank(r.Category), Blank(r.Description), Blank(r.Code), Blank(r.CodeText), Blank(r.RecordedBy), r.LastUpdated);

    private sealed class NoteRow
    {
        public long Id { get; set; }
        public byte[]? Uuid { get; set; }
        public DateTime? Date { get; set; }
        public string? Type { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? Code { get; set; }
        public string? CodeText { get; set; }
        public string? RecordedBy { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
