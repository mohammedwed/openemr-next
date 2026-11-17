using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Hands a new problem a time-ordered v7 uuid. A problem has no human-visible number, so there is no shared counter involved (unlike an encounter).</summary>
public sealed class GuidProblemIdentityAllocator : IProblemIdentityAllocator
{
    public ProblemIdentity Allocate() => ProblemIdentity.Create(Guid.CreateVersion7());
}
