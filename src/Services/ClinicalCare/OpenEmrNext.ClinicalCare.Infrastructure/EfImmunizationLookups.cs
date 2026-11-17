using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IImmunizationLookups"/>: the seven immunization lists from <c>list_options</c> in one parameterized, bounded read, active options only, in <c>seq</c> order. The vaccine list keeps only options
/// whose id is a whole number, because <c>immunization_id</c> is an <c>int</c> column. A blank option id (legacy keeps one as "none") is left out. Capped per list so a misconfigured install cannot make it unbounded.
/// </summary>
public sealed class EfImmunizationLookups(ClinicalCareDbContext db) : IImmunizationLookups
{
    public const int MaxPerList = 500;

    public async Task<ImmunizationOptions> GetOptionsAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<ListRow>(
                $"""
                SELECT list_id AS ListId, option_id AS OptionId, title AS Title
                FROM list_options
                WHERE list_id IN ({EfImmunizationCodeLists.VaccineList}, {EfImmunizationCodeLists.RouteList}, {EfImmunizationCodeLists.SiteList}, {EfImmunizationCodeLists.CompletionList},
                                  {EfImmunizationCodeLists.SourceList}, {EfImmunizationCodeLists.RefusalList}, {EfImmunizationCodeLists.UnitList})
                  AND activity = 1 AND option_id <> ''
                ORDER BY seq, option_id
                """)
            .ToListAsync(ct);

        // The database compares list ids case-insensitively; match likewise.
        IReadOnlyList<LookupOption> Of(string list, bool wholeNumbersOnly = false) =>
            rows.Where(r => string.Equals(r.ListId, list, StringComparison.OrdinalIgnoreCase))
                .Where(r => !wholeNumbersOnly || int.TryParse(r.OptionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Take(MaxPerList)
                .Select(r => new LookupOption(r.OptionId, string.IsNullOrWhiteSpace(r.Title) ? r.OptionId : r.Title))
                .ToList();

        return new ImmunizationOptions(
            Of(EfImmunizationCodeLists.VaccineList, wholeNumbersOnly: true), Of(EfImmunizationCodeLists.RouteList), Of(EfImmunizationCodeLists.SiteList),
            Of(EfImmunizationCodeLists.CompletionList), Of(EfImmunizationCodeLists.SourceList), Of(EfImmunizationCodeLists.RefusalList), Of(EfImmunizationCodeLists.UnitList));
    }

    private sealed class ListRow
    {
        public string? ListId { get; set; }
        public string OptionId { get; set; } = "";
        public string? Title { get; set; }
    }
}
