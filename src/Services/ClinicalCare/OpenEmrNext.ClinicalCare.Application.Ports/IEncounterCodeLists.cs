using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The configured encounter classes (legacy <c>_ActEncounterCode</c>). A port rather than a table read so the
/// coupling to legacy <c>list_options</c> stays inside one adapter and a real Organization service can replace
/// it later (design set R10, decision D6).
/// </summary>
public interface IEncounterCodeLists
{
    /// <returns>Every class in the list, in <c>seq</c> order, with no <c>activity</c> filter: legacy filters on it in neither
    /// the default lookup (<c>ListService::getOptionsByListName</c>) nor the validation (<c>BaseValidator::validateCode</c>).
    /// Empty (not an error) when none are configured.</returns>
    Task<IReadOnlyList<EncounterClassOption>> GetEncounterClassesAsync(CancellationToken ct);
}
