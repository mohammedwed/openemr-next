using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Hands a new allergy a time-ordered v7 uuid. An allergy has no human-visible number, so there is no shared counter involved.</summary>
public sealed class GuidAllergyIdentityAllocator : IAllergyIdentityAllocator
{
    public AllergyIdentity Allocate() => AllergyIdentity.Create(Guid.CreateVersion7());
}
