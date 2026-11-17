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

        // Encounter classes (legacy list _ActEncounterCode). AMB and EMER carry the titles legacy shows;
        // the rest use the standard HL7 ActEncounterCode names. Only a label: the code is what is sent.
        ["AMB"] = "Outpatient",
        ["EMER"] = "Emergency Dept",
        ["ACUTE"] = "Inpatient acute",
        ["FLD"] = "Field",
        ["HH"] = "Home health",
        ["IMP"] = "Inpatient",
        ["NONAC"] = "Inpatient non-acute",
        ["OBSENC"] = "Observation",
        ["PRENC"] = "Pre-admission",
        ["SS"] = "Short stay",
        ["VR"] = "Virtual",
    };

    /// <summary>
    /// The encounter classes the create form offers, in the order legacy lists them with the default (AMB)
    /// first. There is no lookup endpoint yet, so this mirrors the 11 codes in the legacy snapshot; an
    /// unknown code is still rejected by the API (CC-3).
    /// </summary>
    public static readonly IReadOnlyList<string> EncounterClassCodes =
        ["AMB", "EMER", "ACUTE", "FLD", "HH", "IMP", "NONAC", "OBSENC", "PRENC", "SS", "VR"];

    public static string Of(string? code) =>
        code is null ? "—" : Labels.TryGetValue(code, out var label) ? label : code;
}
