using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Reads one encounter by its uuid.</summary>
public sealed class GetEncounterUseCase(IEncounterReader reader)
{
    /// <returns>The encounter, or <c>null</c> when it does not exist or has been deleted in legacy (CC-9).</returns>
    public Task<EncounterView?> ExecuteAsync(Guid encounterUuid, CancellationToken ct) =>
        reader.FindAsync(encounterUuid, ct);
}
