using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.Rules;

/// <summary>
/// Which class code an encounter gets (CC-3). A pure function of the requested code and the configured list.
/// </summary>
public static class EncounterClassRule
{
    /// <summary>
    /// Legacy: the encounter form leaves <c>class_code</c> blank, then takes the <c>_ActEncounterCode</c> entry
    /// flagged <c>is_default</c>, or the first entry when none is flagged (<c>interface/forms/newpatient/save.php:114-130</c>).
    /// A code that is supplied must be in the list (<c>EncounterValidator</c>, <c>validateCode</c> against
    /// <c>list_options</c>). Codes compare exactly, as <c>option_id</c> values do.
    /// </summary>
    /// <returns>The resolved code, or <c>null</c> with a message added to <paramref name="errors"/>.</returns>
    public static string? Resolve(string? requested, IReadOnlyList<EncounterClassOption> classes, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            if (classes.Count == 0)
            {
                errors.Add("No encounter classes are configured, so a default class cannot be chosen.");
                return null;
            }

            return (classes.FirstOrDefault(c => c.IsDefault) ?? classes[0]).Code;
        }

        if (classes.Any(c => string.Equals(c.Code, requested, StringComparison.Ordinal)))
            return requested;

        errors.Add($"Class '{requested}' is not a known encounter class.");
        return null;
    }
}
