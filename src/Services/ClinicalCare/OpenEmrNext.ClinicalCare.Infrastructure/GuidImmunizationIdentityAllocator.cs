using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Allocates a time-ordered version 7 uuid for a new immunization (DOM-7).</summary>
public sealed class GuidImmunizationIdentityAllocator : IImmunizationIdentityAllocator
{
    public ImmunizationIdentity Allocate() => ImmunizationIdentity.Create(Guid.CreateVersion7());
}
