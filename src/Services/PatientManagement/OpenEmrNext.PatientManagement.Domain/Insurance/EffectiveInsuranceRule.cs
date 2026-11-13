using System.Globalization;

namespace OpenEmrNext.PatientManagement.Domain.Insurance;

/// <summary>
/// Which insurance policy is in force for a patient on a day, at each level. A faithful port of
/// legacy OpenEMR's <c>EffectiveInsuranceRule::choose()</c> (src/Services/Insurance/EffectiveInsuranceRule.php),
/// including its handling of MySQL "zero dates" (<c>0000-00-00</c>) and partial dates
/// (<c>2026-05-00</c>) that <c>sql_mode = ''</c> allowed legacy to store -- this project only ever
/// writes clean dates itself, but a patient's existing <c>insurance_data</c> rows may predate this
/// service and still hold them, so the rule has to tolerate the same inputs legacy's own rule does.
/// </summary>
/// <remarks>
/// The rule, verbatim from the legacy source's own doc comment:
/// <list type="bullet">
/// <item>A policy is in force on day D when its start is on or before D and its end is on or after
/// D. Both ends are inclusive.</item>
/// <item>A blank start means always. A blank end, or 0000-00-00, means open.</item>
/// <item>At each level, the policy in force with the latest start wins. A blank start loses to any
/// stored one, 0000-00-00 included; between two blank starts the higher id wins.</item>
/// <item>If that policy has no company ("", "0" or null), the level has no coverage on D -- the
/// "Unassigned" row staff add to end a level.</item>
/// <item>Levels are independent: an ended primary doesn't hide a current secondary. Nothing here
/// computes coordination of benefits.</item>
/// </list>
/// Dates are compared as stored -- the same way SQL compares a DATE column -- not as parsed values;
/// parsing only happens afterward, to describe the winning row's own start/end.
/// </remarks>
public static class EffectiveInsuranceRule
{
    private const string ZeroDate = "0000-00-00";

    public static IReadOnlyList<EffectiveInsurancePolicy> Choose(IReadOnlyList<InsurancePolicyRow> rows, DateOnly day)
    {
        var onDay = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var winners = new Dictionary<InsuranceLevel, (string Start, long Id, EffectiveInsurancePolicy? Policy)>();

        foreach (var row in rows)
        {
            var level = InsuranceLevelExtensions.TryParse(row.Type);
            if (level is null)
                continue;

            // '' for a blank start, which sorts below every stored date and so is in force on any day.
            var start = StoredDate(row.Date);
            var end = StoredDate(row.DateEnd);
            if (end == ZeroDate)
                end = "";

            if (string.CompareOrdinal(start, onDay) > 0 || (end != "" && string.CompareOrdinal(end, onDay) < 0))
                continue;

            if (winners.TryGetValue(level.Value, out var held) && !Outranks(start, row.Id, held.Start, held.Id))
                continue;

            var companyId = StoredId(row.Provider);
            winners[level.Value] = (start, row.Id, companyId is null
                ? null
                : new EffectiveInsurancePolicy(level.Value, row.Id, row.Uuid, companyId.Value, FirstDayOf(start), LastDayOf(end)));
        }

        var inForce = new List<EffectiveInsurancePolicy>();
        foreach (var level in new[] { InsuranceLevel.Primary, InsuranceLevel.Secondary, InsuranceLevel.Tertiary })
        {
            if (winners.TryGetValue(level, out var winner) && winner.Policy is { } policy)
                inForce.Add(policy);
        }

        return inForce;
    }

    /// <summary>Whether a policy outranks the one held for its level: the later start, then the higher id.</summary>
    private static bool Outranks(string start, long id, string heldStart, long heldId)
    {
        var byStart = string.CompareOrdinal(start, heldStart);
        return byStart != 0 ? byStart > 0 : id > heldId;
    }

    /// <summary>A DATE column's value as stored (YYYY-MM-DD, zeros included), or "" when it isn't that shape.</summary>
    private static string StoredDate(string? value) =>
        value is not null && value.Length == 10 && IsYmdShape(value) ? value : "";

    private static bool IsYmdShape(string value) =>
        value[4] == '-' && value[7] == '-'
        && char.IsAsciiDigit(value[0]) && char.IsAsciiDigit(value[1]) && char.IsAsciiDigit(value[2]) && char.IsAsciiDigit(value[3])
        && char.IsAsciiDigit(value[5]) && char.IsAsciiDigit(value[6])
        && char.IsAsciiDigit(value[8]) && char.IsAsciiDigit(value[9]);

    /// <summary>A stored date as a real calendar day, or null when it isn't one (0000-00-00, or 2026-05-00).</summary>
    private static DateOnly? CalendarDay(string stored) =>
        DateOnly.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    /// <summary>
    /// The first calendar day on or after a stored date, as SQL compares them: the date itself when
    /// it is a real day, else the next real one (2026-05-00 gives 2026-05-01; 2026-00-00 gives 2026-01-01).
    /// </summary>
    private static DateOnly FirstDayFrom(string stored)
    {
        if (CalendarDay(stored) is { } day)
            return day;

        var year = int.Parse(stored.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var month = stored.Substring(5, 2);
        var dayOfMonth = stored.Substring(8, 2);

        if (month == "00")
            return new DateOnly(year, 1, 1);

        if (string.CompareOrdinal(month, "12") > 0)
            return new DateOnly(year, 12, 31).AddDays(1);

        var firstOfMonth = new DateOnly(year, int.Parse(month, CultureInfo.InvariantCulture), 1);
        // A zero day comes before the 1st; any other invalid day is past the month's end.
        return dayOfMonth == "00" ? firstOfMonth : firstOfMonth.AddMonths(1);
    }

    /// <summary>The first calendar day after a stored date, as SQL compares them.</summary>
    private static DateOnly FirstDayAfter(string stored) =>
        CalendarDay(stored) is { } day ? day.AddDays(1) : FirstDayFrom(stored);

    /// <summary>A policy's first day in force: null for a blank or 0000-00-00 start (always).</summary>
    private static DateOnly? FirstDayOf(string start) =>
        start == "" || start == ZeroDate ? null : FirstDayFrom(start);

    /// <summary>A policy's last day in force: null for an open end.</summary>
    private static DateOnly? LastDayOf(string end) =>
        end == "" ? null : FirstDayAfter(end).AddDays(-1);

    /// <summary>
    /// A stored id as a positive number: digits only (leading zeros and surrounding whitespace
    /// allowed), anything else -- blank, "0", non-numeric -- is no id/company. Matches legacy's
    /// <c>EffectiveInsuranceRule::storedId()</c> exactly; it is this permissive specifically for
    /// <c>provider</c>, where "no company" is a real, meaningful state (the "Unassigned" row).
    /// </summary>
    public static long? StoredId(string? value)
    {
        if (value is null)
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiDigit))
            return null;

        var stripped = trimmed.TrimStart('0');
        return stripped.Length > 0 && long.TryParse(stripped, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
