using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IVitalsIdentityAllocator"/>: a version 7 uuid, timestamp-first like legacy's COMB
/// uuids so rows stay roughly time-ordered in the unique index. Randomness lives here, never in the domain (DOM-8).
/// </summary>
public sealed class GuidVitalsIdentityAllocator : IVitalsIdentityAllocator
{
    public VitalsIdentity Allocate() => VitalsIdentity.Create(Guid.CreateVersion7());
}
