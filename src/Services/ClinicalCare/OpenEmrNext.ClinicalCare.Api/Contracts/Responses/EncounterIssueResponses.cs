using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>What <c>PUT /api/v1/encounters/{encounterUuid}/issues/{issueUuid}</c> returns: the link, new (201) or already there (200).</summary>
/// <param name="LinkUuid">The link's own uuid.</param>
/// <param name="EncounterUuid">The encounter the issue is linked to.</param>
/// <param name="IssueUuid">The problem or allergy linked.</param>
/// <param name="Resolved">The stored resolved flag: what was asked for if the link is new, what was already stored if it is not.</param>
public sealed record EncounterIssueLinkResponse(Guid? LinkUuid, Guid EncounterUuid, Guid IssueUuid, bool Resolved);

/// <summary>One issue linked to an encounter, as the API returns it.</summary>
/// <param name="LinkUuid">The link's own uuid, if legacy gave the row one.</param>
/// <param name="IssueUuid">The problem or allergy's uuid: read it from <c>/api/v1/problems/{uuid}</c> or <c>/api/v1/allergies/{uuid}</c> according to <paramref name="Kind"/>.</param>
/// <param name="Kind"><c>problem</c> or <c>allergy</c>.</param>
/// <param name="Title">The issue's title.</param>
/// <param name="Resolved">Whether the issue was resolved during this encounter.</param>
/// <param name="LinkedAt">When the link was made, if recorded.</param>
public sealed record EncounterIssueResponse(Guid? LinkUuid, Guid IssueUuid, string Kind, string? Title, bool Resolved, DateTime? LinkedAt)
{
    public static EncounterIssueResponse From(EncounterIssueView view) => new(
        view.LinkUuid, view.IssueUuid, view.Kind == IssueKind.Allergy ? "allergy" : "problem", view.Title, view.Resolved, view.LinkedAt);
}

/// <summary>One bounded page of the issues linked to an encounter, with the total so a caller can page.</summary>
public sealed record EncounterIssueListResponse(IReadOnlyList<EncounterIssueResponse> Items, int Total, int Limit, int Offset);
