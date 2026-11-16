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
