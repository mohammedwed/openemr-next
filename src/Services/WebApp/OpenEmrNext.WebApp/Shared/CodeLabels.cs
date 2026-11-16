namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// Readable names for the codes the API stores (insurance levels, document types, directive status).
/// Unknown codes fall back to the raw value rather than hiding it.
/// </summary>
public static class CodeLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["primary"] = "Primary",
        ["secondary"] = "Secondary",
        ["tertiary"] = "Tertiary",
        ["living_will"] = "Living will",
        ["durable_power_attorney"] = "Durable power of attorney",
        ["dnr_order"] = "DNR order",
        ["yes"] = "Yes",
        ["no"] = "No",
        ["unknown"] = "Unknown",
    };

    public static string Of(string? code) =>
        code is null ? "—" : Labels.TryGetValue(code, out var label) ? label : code;
}
