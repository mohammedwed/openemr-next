namespace OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;

/// <summary>
/// The order staff gave a policy. Matches legacy OpenEMR's own <c>insurance_data.type</c> enum
/// exactly (<c>InsuranceLevel.php</c>) -- OpenEMR doesn't compute coordination of benefits; the
/// stored level is the order.
/// </summary>
public enum InsuranceLevel
{
    Primary,
    Secondary,
    Tertiary,
}

public static class InsuranceLevelExtensions
{
    public static string ToWireValue(this InsuranceLevel level) => level switch
    {
        InsuranceLevel.Primary => "primary",
        InsuranceLevel.Secondary => "secondary",
        InsuranceLevel.Tertiary => "tertiary",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>Matches legacy's <c>InsuranceLevel::tryFrom()</c>: null for anything that isn't exactly one of the three stored values.</summary>
    public static InsuranceLevel? TryParse(string? value) => value switch
    {
        "primary" => InsuranceLevel.Primary,
        "secondary" => InsuranceLevel.Secondary,
        "tertiary" => InsuranceLevel.Tertiary,
        _ => null,
    };
}
