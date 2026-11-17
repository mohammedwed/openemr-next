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

        // Allergy and problem codes (legacy lists reaction, severity_ccda, allergyintolerance-verification and condition-verification).
        // Only a label: the code is what is sent, and a code the site has not configured is rejected by the API.
        ["unassigned"] = "Unassigned",
        ["hives"] = "Hives",
        ["nausea"] = "Nausea",
        ["shortness_of_breath"] = "Shortness of breath",
        ["mild"] = "Mild",
        ["mild_to_moderate"] = "Mild to moderate",
        ["moderate"] = "Moderate",
        ["moderate_to_severe"] = "Moderate to severe",
        ["severe"] = "Severe",
        ["life_threatening_severity"] = "Life threatening",
        ["fatal"] = "Fatal",
        ["unconfirmed"] = "Unconfirmed",
        ["confirmed"] = "Confirmed",
        ["refuted"] = "Refuted",
        ["entered-in-error"] = "Entered in error",
    };

    /// <summary>
    /// The problem outcomes the forms offer (legacy list <c>outcome</c>), 0 first. There is no lookup endpoint yet, so this mirrors the snapshot; an unconfigured code is still
    /// rejected by the API (PB-5). 1 is "Resolved": the quick Resolve action sets it together with an end date.
    /// </summary>
    public static readonly IReadOnlyList<(int Code, string Label)> ProblemOutcomes =
        [(0, "Unassigned"), (1, "Resolved"), (2, "Improved"), (3, "Status quo"), (4, "Worse"), (5, "Pending followup")];

    /// <summary>The problem occurrences the forms offer (legacy list <c>occurrence</c>), 0 first. Mirrors the snapshot; the API checks the real list (PB-6).</summary>
    public static readonly IReadOnlyList<(int Code, string Label)> ProblemOccurrences =
        [(0, "Unknown or N/A"), (1, "First"), (4, "Chronic/recurrent"), (5, "Acute on chronic"), (6, "Early recurrence (under 2 months)"),
         (7, "Late recurrence (2 to 12 months)"), (8, "Delayed recurrence (over 12 months)")];

    /// <summary>The allergy reactions the form offers (legacy list <c>reaction</c>). Mirrors the snapshot; the API checks the real list (AL-4).</summary>
    public static readonly IReadOnlyList<string> AllergyReactionCodes = ["unassigned", "hives", "nausea", "shortness_of_breath"];

    /// <summary>The allergy severities the form offers (legacy list <c>severity_ccda</c>). Mirrors the snapshot; the API checks the real list (AL-5).</summary>
    public static readonly IReadOnlyList<string> SeverityCodes =
        ["unassigned", "mild", "mild_to_moderate", "moderate", "moderate_to_severe", "severe", "life_threatening_severity", "fatal"];

    /// <summary>The verification statuses both an allergy and a problem offer (their two lists hold the same four codes in the snapshot, but are separate lists to the API: AL-6, PB-21).</summary>
    public static readonly IReadOnlyList<string> VerificationCodes = ["unconfirmed", "confirmed", "refuted", "entered-in-error"];

    public static string OutcomeOf(int code) => ProblemOutcomes.FirstOrDefault(o => o.Code == code).Label ?? $"Outcome {code}";

    public static string OccurrenceOf(int code) => ProblemOccurrences.FirstOrDefault(o => o.Code == code).Label ?? $"Occurrence {code}";

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
