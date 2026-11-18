namespace OpenEmrNext.WebApp.Shared;

/// <summary>Display helpers shared by the patient pages.</summary>
public static class Formatting
{
    /// <summary>"11 B", "1.5 KB", "2.0 MB": binary units, one decimal place above bytes.</summary>
    public static string FileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / kb:0.0} KB",
            _ => $"{bytes / mb:0.0} MB",
        };
    }

    /// <summary>
    /// "2026-11-17 09:30 UTC", or a dash when missing. Shows the digits the API returned, not a conversion: times are UTC everywhere in
    /// this app (<see cref="UtcTime"/>), so the digits are already what the user entered, and the suffix says which clock they are on.
    /// </summary>
    public static string DateTimeText(DateTimeOffset? value) =>
        value is { } v ? v.DateTime.ToString("yyyy-MM-dd HH:mm") + " UTC" : "—";

    /// <summary>"2026-11-17", or a dash when missing. Same no-conversion rule as <see cref="DateTimeText"/>.</summary>
    public static string DateText(DateTimeOffset? value) =>
        value is { } v ? v.DateTime.ToString("yyyy-MM-dd") : "—";

    /// <summary>
    /// "154.3 lb", "37 °C", or a dash when there is no value. One decimal at most: the service converts units
    /// with six decimals, which reads as noise ("175.000001 cm") on screen.
    /// </summary>
    public static string Measurement(double? value, string? unit)
    {
        if (value is not { } v)
        {
            return "—";
        }

        var text = Math.Round(v, 1).ToString("0.#");
        return unit is null ? text : $"{text} {UnitSymbol(unit)}";
    }

    /// <summary>"120/80 mmHg"; a dash when neither reading exists, and "?" for a missing half.</summary>
    public static string BloodPressure(int? systolic, int? diastolic) =>
        systolic is null && diastolic is null ? "—" : $"{systolic?.ToString() ?? "?"}/{diastolic?.ToString() ?? "?"} mmHg";

    private static string UnitSymbol(string unit) => unit switch
    {
        "degF" => "°F",
        "degC" => "°C",
        _ => unit,
    };

    /// <summary>"1990-01-01 (36 years)", or just the date when it's missing. Age counts completed years.</summary>
    public static string DateOfBirthWithAge(DateTimeOffset? dob, DateOnly today)
    {
        if (dob is not { } value)
        {
            return "—";
        }

        var birth = DateOnly.FromDateTime(value.DateTime);
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age))
        {
            age--;
        }

        return $"{birth:yyyy-MM-dd} ({age} years)";
    }
}
