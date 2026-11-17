namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The optional body of <c>PUT /api/v1/encounters/{encounterUuid}/issues/{issueUuid}</c>. The whole body may be omitted.
/// </summary>
/// <param name="Resolved">
/// Whether the issue was resolved during this encounter (legacy's <c>issue_encounter.resolved</c>). It is used only when the link is <b>new</b>: a link is never updated, so on an
/// existing link this is ignored and the stored value comes back. To change it, unlink and link again.
/// </param>
public sealed record LinkIssueRequest(bool Resolved = false);
