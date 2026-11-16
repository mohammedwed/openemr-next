using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Hands out the identity of new vitals (DOM-7, DOM-8): a fresh uuid. Randomness is an input to the domain, so it comes
/// from here. Unlike an encounter there is no legacy number to draw from the shared counter.
/// </summary>
public interface IVitalsIdentityAllocator
{
    VitalsIdentity Allocate();
}
