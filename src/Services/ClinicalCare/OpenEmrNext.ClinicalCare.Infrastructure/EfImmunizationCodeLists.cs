using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IImmunizationCodeLists"/>: seven lists from legacy's <c>list_options</c>, one parameterized read. Option ids are the exact strings legacy stores and compares; the vaccine list's
/// ids are kept only when they are whole numbers, because <c>immunization_id</c> is an <c>int</c> column.
/// </summary>
public sealed class EfImmunizationCodeLists(ClinicalCareDbContext db) : IImmunizationCodeLists
{
    public const string VaccineList = "immunizations", RouteList = "drug_route", SiteList = "immunization_administered_site",
        CompletionList = "Immunization_Completion_Status", SourceList = "immunization_informationsource", RefusalList = "immunization_refusal_reason",
        UnitList = "drug_units";

    public async Task<ImmunizationCodeData> GetCodesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<ListRow>(
                $"""
                SELECT list_id AS ListId, option_id AS OptionId
                FROM list_options
                WHERE list_id IN ({VaccineList}, {RouteList}, {SiteList}, {CompletionList}, {SourceList}, {RefusalList}, {UnitList})
                """)
            .ToListAsync(ct);

        // The database compares list ids case-insensitively, so the same list may come back spelled differently from the constant: match likewise.
        HashSet<string> Codes(string list) =>
            rows.Where(r => string.Equals(r.ListId, list, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(r.OptionId))
                .Select(r => r.OptionId!).ToHashSet(StringComparer.Ordinal);

        return new ImmunizationCodeData(
            Codes(VaccineList).Select(c => int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (int?)n : null)
                .Where(n => n is not null).Select(n => n!.Value).ToHashSet(),
            Codes(RouteList), Codes(SiteList), Codes(CompletionList), Codes(SourceList), Codes(RefusalList), Codes(UnitList));
    }

    private sealed class ListRow
    {
        public string? ListId { get; set; }
        public string? OptionId { get; set; }
    }
}
