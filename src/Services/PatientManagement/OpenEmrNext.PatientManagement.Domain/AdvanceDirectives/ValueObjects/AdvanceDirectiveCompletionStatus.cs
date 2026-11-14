namespace OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;

/// <summary>
/// Legacy's <c>patient_data.completed_ad</c> is a <c>VARCHAR(3) NOT NULL DEFAULT 'NO'</c> fed from the
/// <c>yesno</c> list (<c>list_options</c>: option ids <c>'YES'</c>/<c>'NO'</c>), but the edit screen
/// (<c>advancedirectives.php</c>) also offers a blank "SKIP" choice, which legacy happily stores as the
/// empty string -- a third, real state ("staff hasn't said either way"), not a bug to normalize away.
/// </summary>
public enum AdvanceDirectiveCompletionStatus
{
    Unknown,
    Yes,
    No
}

public static class AdvanceDirectiveCompletionStatusExtensions
{
    public static string ToStoredValue(this AdvanceDirectiveCompletionStatus status) => status switch
    {
        AdvanceDirectiveCompletionStatus.Yes => "YES",
        AdvanceDirectiveCompletionStatus.No => "NO",
        AdvanceDirectiveCompletionStatus.Unknown => "",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static AdvanceDirectiveCompletionStatus FromStoredValue(string? storedValue) => storedValue switch
    {
        "YES" => AdvanceDirectiveCompletionStatus.Yes,
        "NO" => AdvanceDirectiveCompletionStatus.No,
        _ => AdvanceDirectiveCompletionStatus.Unknown
    };

    /// <summary>The HTTP wire form -- lowercase, unlike <see cref="ToStoredValue"/>'s legacy-matching casing.</summary>
    public static string ToWireValue(this AdvanceDirectiveCompletionStatus status) => status switch
    {
        AdvanceDirectiveCompletionStatus.Yes => "yes",
        AdvanceDirectiveCompletionStatus.No => "no",
        AdvanceDirectiveCompletionStatus.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static AdvanceDirectiveCompletionStatus? TryParseWireValue(string? wireValue) => wireValue switch
    {
        "yes" => AdvanceDirectiveCompletionStatus.Yes,
        "no" => AdvanceDirectiveCompletionStatus.No,
        "unknown" => AdvanceDirectiveCompletionStatus.Unknown,
        _ => null
    };
}
