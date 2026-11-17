using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IClinicalNoteCodeLists"/>: the <c>Clinical_Note_Type</c> and <c>Clinical_Note_Category</c> lists from legacy's <c>list_options</c>, one parameterized read, in <c>seq</c> order.
/// A type's code is the LOINC value legacy keeps in the option's <c>notes</c> column (<c>LOINC:11506-3</c>); an option without one has an empty code, as legacy stores it.
/// </summary>
public sealed class EfClinicalNoteCodeLists(ClinicalCareDbContext db) : IClinicalNoteCodeLists
{
    public const string TypeList = "Clinical_Note_Type", CategoryList = "Clinical_Note_Category";

    public async Task<ClinicalNoteCodeData> GetCodesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<ListRow>(
                $"""
                SELECT list_id AS ListId, option_id AS OptionId, title AS Title, notes AS Notes, CAST(activity AS SIGNED) AS Activity
                FROM list_options
                WHERE list_id IN ({TypeList}, {CategoryList}) AND option_id <> ''
                ORDER BY seq, option_id
                """)
            .ToListAsync(ct);

        // The database compares list ids case-insensitively; match likewise.
        bool Is(ListRow r, string list) => string.Equals(r.ListId, list, StringComparison.OrdinalIgnoreCase);

        return new ClinicalNoteCodeData(
            rows.Where(r => Is(r, TypeList)).Select(r => new ClinicalNoteTypeOption(r.OptionId, r.Title ?? r.OptionId, r.Notes ?? "", r.Activity != 0)).ToList(),
            rows.Where(r => Is(r, CategoryList)).Select(r => new ClinicalNoteCategoryOption(r.OptionId, r.Title ?? r.OptionId, r.Activity != 0)).ToList());
    }

    private sealed class ListRow
    {
        public string? ListId { get; set; }
        public string OptionId { get; set; } = "";
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public long Activity { get; set; }
    }
}
