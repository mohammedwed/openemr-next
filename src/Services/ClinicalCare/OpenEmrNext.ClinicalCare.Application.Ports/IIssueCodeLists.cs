using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The configured code lists an issue's coded fields are checked against (legacy <c>list_options</c>: <c>outcome</c>, <c>occurrence</c>). A port rather
/// than a table read so the coupling to legacy stays in one adapter and a real terminology service can replace it later (design set decision D6).
/// </summary>
public interface IIssueCodeLists
{
    /// <returns>The valid codes of every list, with no <c>activity</c> filter (legacy filters on it in neither lookup). Empty sets (not an error) when none are configured.</returns>
    Task<IssueCodeData> GetCodesAsync(CancellationToken ct);
}
