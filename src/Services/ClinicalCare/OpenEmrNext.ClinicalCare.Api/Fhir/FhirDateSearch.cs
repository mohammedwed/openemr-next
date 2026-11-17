using System.Globalization;
using System.Text.RegularExpressions;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Fhir;

/// <summary>
/// Reads a FHIR date search value (<c>_lastUpdated=ge2026-03-01</c>) into a window on a stamp (ADR 0079 #6). A value has a precision, and the precision is the size of the range it means: <c>2026</c> is the year,
/// <c>2026-03</c> the month, <c>2026-03-15</c> the day, a time to the minute or the second that minute or second. Against that range <c>eq</c> is "inside it", <c>gt</c> "after its end", <c>ge</c> "from its start",
/// <c>lt</c> "before its start" and <c>le</c> "up to its end". The prefixes <c>ne</c>, <c>sa</c>, <c>eb</c> and <c>ap</c> are refused, not approximated. A value with a zone is converted to the server's local time, which is
/// what the stamps are in; one without is taken to be local.
/// </summary>
internal static partial class FhirDateSearch
{
    [GeneratedRegex(@"^(?<prefix>eq|ne|gt|lt|ge|le|sa|eb|ap)?(?<date>\d{4}(-\d{2}(-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?)?)?)?)$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?) (\d{2}:\d{2})$")]
    private static partial Regex LostPlus();

    /// <returns>The window, or a message saying what is wrong with the value.</returns>
    public static bool TryParse(string raw, out UpdatedWindow window, out string problem)
    {
        window = new UpdatedWindow(null, null);
        problem = "";

        // A "+" in a query string reaches the service as a space; an offset like +02:00 is the only place one is meaningful here.
        var text = LostPlus().Replace(raw.Trim(), "$1+$4");
        var match = Pattern().Match(text);
        if (!match.Success)
        {
            problem = $"'{raw}' is not a date this search understands: use a year, a month, a day or a date and time (2026, 2026-03, 2026-03-15, 2026-03-15T10:30:00Z), with an optional prefix of eq, gt, ge, lt or le.";
            return false;
        }

        var prefix = match.Groups["prefix"].Success ? match.Groups["prefix"].Value : "eq";
        if (prefix is "ne" or "sa" or "eb" or "ap")
        {
            problem = $"The search prefix '{prefix}' is not supported: use eq, gt, ge, lt or le.";
            return false;
        }

        if (!TryRange(match.Groups["date"].Value, out var start, out var end))
        {
            problem = $"'{raw}' is not a real date.";
            return false;
        }

        window = prefix switch
        {
            "gt" => new UpdatedWindow(end, null),
            "ge" => new UpdatedWindow(start, null),
            "lt" => new UpdatedWindow(null, start),
            "le" => new UpdatedWindow(null, end),
            _ => new UpdatedWindow(start, end)
        };
        return true;
    }

    /// <summary>The range the value stands for, in the server's local time: its start, and the first moment after it.</summary>
    private static bool TryRange(string date, out DateTime start, out DateTime end)
    {
        start = end = default;
        try
        {
            switch (date.Length)
            {
                case 4:
                    start = new DateTime(int.Parse(date, CultureInfo.InvariantCulture), 1, 1);
                    end = start.AddYears(1);
                    return true;
                case 7:
                    start = DateTime.ParseExact(date, "yyyy-MM", CultureInfo.InvariantCulture);
                    end = start.AddMonths(1);
                    return true;
                case 10:
                    start = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    end = start.AddDays(1);
                    return true;
            }

            // A date and time. With a zone it is an instant, converted to local time; without one it is already local.
            var hasZone = date.EndsWith('Z') || Regex.IsMatch(date, @"[+-]\d{2}:\d{2}$");
            var parsed = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture, hasZone ? DateTimeStyles.None : DateTimeStyles.AssumeLocal);
            start = parsed.ToLocalTime().DateTime;

            // The precision of a time: to the minute when there are no seconds, otherwise to the second (a fraction is a second: the stamps are not finer).
            var timePart = date[(date.IndexOf('T') + 1)..];
            var hasSeconds = Regex.IsMatch(timePart, @"^\d{2}:\d{2}:\d{2}");
            start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, hasSeconds ? start.Second : 0);
            end = hasSeconds ? start.AddSeconds(1) : start.AddMinutes(1);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            return false;
        }
    }
}
