using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Hands a new link a time-ordered v7 uuid (legacy's <c>issue_encounter</c> row has a uuid of its own).</summary>
public sealed class GuidEncounterIssueLinkIdentityAllocator : IEncounterIssueLinkIdentityAllocator
{
    public EncounterIssueLinkIdentity Allocate() => EncounterIssueLinkIdentity.Create(Guid.CreateVersion7());
}
