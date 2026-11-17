using OpenEmrNext.ClinicalCare.Domain.Problems;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>Hands a new problem its identity (DOM-7). A problem has only a uuid, so there is no shared counter to take a number from.</summary>
public interface IProblemIdentityAllocator
{
    ProblemIdentity Allocate();
}
