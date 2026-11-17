using OpenEmrNext.ClinicalCare.Domain.IssueLinks;

namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// One issue linked to an encounter, as a caller reads it: the link and a summary of the problem or allergy it points at. A query result, not an aggregate (ADR 0054).
/// Only the two issue types this service owns appear; a link to a medication or a surgery is not addressable here (ADR 0070 #1).
/// </summary>
/// <param name="LinkUuid">The link's own uuid, or <c>null</c> for a link row legacy created without one.</param>
/// <param name="IssueUuid">The issue's public identity.</param>
/// <param name="Kind">Problem or allergy.</param>
/// <param name="Title">The issue's title, if it has one.</param>
/// <param name="Resolved">Whether the issue was resolved during this encounter.</param>
/// <param name="LinkedAt">When the link was made, if recorded.</param>
public sealed record EncounterIssueView(Guid? LinkUuid, Guid IssueUuid, IssueKind Kind, string? Title, bool Resolved, DateTime? LinkedAt);

/// <summary>One page of an encounter's linked issues, with the total so a caller can page.</summary>
public sealed record EncounterIssuePage(IReadOnlyList<EncounterIssueView> Items, int Total);
