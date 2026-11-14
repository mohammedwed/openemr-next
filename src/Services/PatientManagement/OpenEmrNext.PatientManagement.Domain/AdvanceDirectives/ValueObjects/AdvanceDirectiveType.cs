namespace OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;

/// <summary>
/// The three directive kinds legacy OpenEMR's <c>PatientAdvanceDirectiveService::ADVANCE_DIRECTIVE_TYPES</c>
/// recognizes. Legacy has no dedicated "type" column for these -- it classifies a <c>documents</c> row
/// purely by matching keywords in the document's own <c>name</c> (or its category's name), so
/// <see cref="ClassifyFromName"/> ports that exact matching for reading rows back, including
/// pre-existing legacy rows this service never wrote itself.
/// </summary>
public enum AdvanceDirectiveType
{
    LivingWill,
    DurablePowerOfAttorney,
    DnrOrder
}

public static class AdvanceDirectiveTypeExtensions
{
    /// <summary>The exact display text legacy's <c>ADVANCE_DIRECTIVE_TYPES</c> map uses -- also the
    /// keyword-bearing text <see cref="ClassifyFromName"/> matches back against, so a document this
    /// service creates is classified correctly by legacy's own code too.</summary>
    public static string ToDisplayName(this AdvanceDirectiveType type) => type switch
    {
        AdvanceDirectiveType.LivingWill => "Living Will",
        AdvanceDirectiveType.DurablePowerOfAttorney => "Durable Power of Attorney",
        AdvanceDirectiveType.DnrOrder => "Do Not Resuscitate Order",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>The wire/key form, matching <c>ADVANCE_DIRECTIVE_TYPES</c>' array keys.</summary>
    public static string ToWireValue(this AdvanceDirectiveType type) => type switch
    {
        AdvanceDirectiveType.LivingWill => "living_will",
        AdvanceDirectiveType.DurablePowerOfAttorney => "durable_power_attorney",
        AdvanceDirectiveType.DnrOrder => "dnr_order",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static AdvanceDirectiveType? TryParse(string? wireValue) => wireValue switch
    {
        "living_will" => AdvanceDirectiveType.LivingWill,
        "durable_power_attorney" => AdvanceDirectiveType.DurablePowerOfAttorney,
        "dnr_order" => AdvanceDirectiveType.DnrOrder,
        _ => null
    };

    /// <summary>
    /// Ports <c>PatientAdvanceDirectiveService::determineAdvanceDirectiveType()</c> exactly: name first,
    /// category second, same substring keywords, same fallback to "Advance Directive" (returned here as
    /// <c>null</c> -- the generic bucket legacy itself can't narrow down to one of the three types).
    /// </summary>
    public static AdvanceDirectiveType? ClassifyFromName(string? name, string? categoryName = null)
    {
        var nameLower = (name ?? "").ToLowerInvariant();
        var categoryLower = (categoryName ?? "").ToLowerInvariant();

        if (nameLower.Contains("living") && nameLower.Contains("will"))
            return AdvanceDirectiveType.LivingWill;
        if (nameLower.Contains("durable") && nameLower.Contains("power"))
            return AdvanceDirectiveType.DurablePowerOfAttorney;
        if (nameLower.Contains("dnr") || nameLower.Contains("do not resuscitate"))
            return AdvanceDirectiveType.DnrOrder;

        if (categoryLower.Contains("living will"))
            return AdvanceDirectiveType.LivingWill;
        if (categoryLower.Contains("durable power of attorney"))
            return AdvanceDirectiveType.DurablePowerOfAttorney;
        if (categoryLower.Contains("do not resuscitate"))
            return AdvanceDirectiveType.DnrOrder;

        return null;
    }
}
