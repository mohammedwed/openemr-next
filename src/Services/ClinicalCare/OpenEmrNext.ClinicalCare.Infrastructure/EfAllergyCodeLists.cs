using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAllergyCodeLists"/>: reads the <c>reaction</c>, <c>severity_ccda</c> and <c>allergyintolerance-verification</c> lists from
/// legacy's <c>list_options</c>, one parameterized read. Option ids are kept as the exact strings legacy stores and compares.
/// </summary>
public sealed class EfAllergyCodeLists(ClinicalCareDbContext db) : IAllergyCodeLists
{
    public const string ReactionList = "reaction";
    public const string SeverityList = "severity_ccda";
    public const string VerificationList = "allergyintolerance-verification";

    public async Task<AllergyCodeData> GetCodesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<ListRow>(
                $"""
                SELECT list_id AS ListId, option_id AS OptionId
                FROM list_options
                WHERE list_id IN ({ReactionList}, {SeverityList}, {VerificationList})
                """)
            .ToListAsync(ct);

        return new AllergyCodeData(CodesOf(rows, ReactionList), CodesOf(rows, SeverityList), CodesOf(rows, VerificationList));
    }

    private static HashSet<string> CodesOf(List<ListRow> rows, string list) =>
        rows.Where(r => r.ListId == list && !string.IsNullOrEmpty(r.OptionId)).Select(r => r.OptionId!).ToHashSet(StringComparer.Ordinal);

    private sealed class ListRow
    {
        public string? ListId { get; set; }
        public string? OptionId { get; set; }
    }
}
