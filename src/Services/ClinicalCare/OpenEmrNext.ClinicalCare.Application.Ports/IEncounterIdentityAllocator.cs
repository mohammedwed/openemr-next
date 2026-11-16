using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Hands out the identity of a new encounter (DOM-7): a fresh uuid, and the legacy encounter number from the
/// shared <c>sequences</c> counter every legacy writer also uses (CC-5, LEG-12), never <c>MAX + 1</c>.
/// </summary>
/// <remarks>
/// Called only after the request has been validated, so a rejected request never consumes a number. A number
/// that is allocated and then not used (a failed insert) is simply skipped, exactly as in legacy.
/// </remarks>
public interface IEncounterIdentityAllocator
{
    Task<EncounterIdentity> AllocateAsync(CancellationToken ct);
}
