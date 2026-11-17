using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IIssueCodeLists"/>: reads the <c>outcome</c> and <c>occurrence</c> lists from legacy's <c>list_options</c>. One
/// parameterized read of two lists. Legacy stores these codes in <c>int</c> columns, so an entry whose <c>option_id</c> is not a whole number cannot
/// be stored in one and is left out rather than failing the request.
/// </summary>
public sealed class EfIssueCodeLists(ClinicalCareDbContext db) : IIssueCodeLists
{
    public const string OutcomeList = "outcome";
    public const string OccurrenceList = "occurrence";
    public const string ConditionVerificationList = "condition-verification";

    public async Task<IssueCodeData> GetCodesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<ListRow>(
                $"""
                SELECT list_id AS ListId, option_id AS OptionId
                FROM list_options
                WHERE list_id IN ({OutcomeList}, {OccurrenceList}, {ConditionVerificationList})
                """)
            .ToListAsync(ct);

        return new IssueCodeData(
            CodesOf(rows, OutcomeList), CodesOf(rows, OccurrenceList),
            rows.Where(r => r.ListId == ConditionVerificationList && !string.IsNullOrEmpty(r.OptionId)).Select(r => r.OptionId!).ToHashSet(StringComparer.Ordinal));
    }

    private static HashSet<int> CodesOf(List<ListRow> rows, string list) =>
        rows.Where(r => r.ListId == list)
            .Select(r => int.TryParse(r.OptionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? (int?)code : null)
            .Where(code => code is not null)
            .Select(code => code!.Value)
            .ToHashSet();

    private sealed class ListRow
    {
        public string? ListId { get; set; }
        public string? OptionId { get; set; }
    }
}
