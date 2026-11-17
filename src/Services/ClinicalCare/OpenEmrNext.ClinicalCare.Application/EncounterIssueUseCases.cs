using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Everything a caller supplies to link an issue to an encounter.</summary>
/// <param name="Resolved">Whether the issue was resolved during the encounter. Used only if the link is new: an existing link is returned as it is.</param>
public sealed record LinkIssueToEncounterCommand(Guid EncounterUuid, Guid IssueUuid, bool Resolved, RequestOrigin? Origin = null);

/// <summary>What a link call did.</summary>
/// <param name="Created">Whether this call made the link (201) rather than finding it (200).</param>
/// <param name="LinkUuid">The link's uuid.</param>
/// <param name="Resolved">The stored resolved flag.</param>
/// <param name="IssueUuid">The issue linked.</param>
/// <param name="EncounterUuid">The encounter it is linked to.</param>
public sealed record IssueLinkResult(bool Created, Guid? LinkUuid, bool Resolved, Guid IssueUuid, Guid EncounterUuid);

/// <summary>
/// Links a problem or an allergy to an encounter that addressed it (M5c slice 4). Thin by design (ARC-4): resolve the encounter and the issue through ports, let the domain
/// decide (same patient), hand the result to <see cref="IEncounterIssueRegistry"/> to commit with its event and audit row. Idempotent, as legacy's is. A refused request is
/// audited as failed against the encounter (ADR 0069 #4).
/// </summary>
public sealed class LinkIssueToEncounterUseCase(
    IEncounterReader encounters,
    IIssueDirectory issues,
    IEncounterIssueLinkIdentityAllocator identities,
    IEncounterIssueRegistry registry,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="IssueNotFoundException">No such problem or allergy. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="ClinicalCareValidationException">The encounter has no number or no known patient, or the issue is another patient's. A <c>failed</c> audit row is written.</exception>
    public async Task<IssueLinkResult> ExecuteAsync(LinkIssueToEncounterCommand command, CancellationToken ct)
    {
        try
        {
            return await LinkAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or IssueNotFoundException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.EncounterIssueLink, command.EncounterUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<IssueLinkResult> LinkAsync(LinkIssueToEncounterCommand command, CancellationToken ct)
    {
        var encounter = await encounters.FindAsync(command.EncounterUuid, ct)
            ?? throw new EncounterNotFoundException(command.EncounterUuid);
        var issue = await issues.FindAsync(command.IssueUuid, ct)
            ?? throw new IssueNotFoundException(command.IssueUuid);

        var link = EncounterIssueLink.Create(
            encounter.Uuid, encounter.Number, encounter.PatientUuid, encounter.PatientPid, issue, command.Resolved, identities.Allocate());

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var registration = await registry.LinkAsync(
            link,
            new IssueLinkedToEncounterEvent(
                link.Uuid, link.EncounterUuid, link.EncounterNumber, issue.Uuid, KindName(issue.Kind), link.Patient.Uuid, link.Patient.Pid, nowUtc),
            new AuditEntry(
                nowUtc, AuditActor.Unknown, AuditActions.EncounterIssueLink, command.EncounterUuid, AuditOutcome.Success,
                command.Origin?.CorrelationId, command.Origin?.SourceAddress),
            ct);

        return new IssueLinkResult(registration.Created, registration.LinkUuid, registration.Resolved, issue.Uuid, encounter.Uuid);
    }

    internal static string KindName(IssueKind kind) => kind == IssueKind.Problem ? "problem" : "allergy";
}

/// <summary>Everything a caller supplies to unlink an issue from an encounter.</summary>
public sealed record UnlinkIssueFromEncounterCommand(Guid EncounterUuid, Guid IssueUuid, RequestOrigin? Origin = null);

/// <summary>
/// Removes the link between an encounter and an issue. It removes the <b>link</b>, never the issue or the encounter, which is why it is not a delete of clinical data; legacy's
/// encounter screen does the same on every save. Idempotent: an absent link is not an error, and nothing is written or audited for it.
/// </summary>
public sealed class UnlinkIssueFromEncounterUseCase(
    IEncounterReader encounters,
    IIssueDirectory issues,
    IEncounterIssueRegistry registry,
    IAuditLog auditLog,
    TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it. A <c>failed</c> audit row is written.</exception>
    /// <exception cref="IssueNotFoundException">No such problem or allergy. A <c>failed</c> audit row is written.</exception>
    /// <returns><c>true</c> if a link was removed; <c>false</c> if there was none.</returns>
    public async Task<bool> ExecuteAsync(UnlinkIssueFromEncounterCommand command, CancellationToken ct)
    {
        try
        {
            return await UnlinkAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or IssueNotFoundException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.EncounterIssueUnlink, command.EncounterUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<bool> UnlinkAsync(UnlinkIssueFromEncounterCommand command, CancellationToken ct)
    {
        var encounter = await encounters.FindAsync(command.EncounterUuid, ct)
            ?? throw new EncounterNotFoundException(command.EncounterUuid);
        var issue = await issues.FindAsync(command.IssueUuid, ct)
            ?? throw new IssueNotFoundException(command.IssueUuid);

        // A legacy-created encounter with no number or no patient key cannot have a link this service could have made, so there is nothing to remove.
        if (encounter.Number is not > 0 || encounter.PatientPid is not > 0)
            return false;

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        return await registry.UnlinkAsync(
            new UnlinkRequest(
                encounter.Uuid, encounter.Number.Value, encounter.PatientPid.Value, issue,
                new IssueUnlinkedFromEncounterEvent(
                    encounter.Uuid, encounter.Number.Value, issue.Uuid, LinkIssueToEncounterUseCase.KindName(issue.Kind), encounter.PatientUuid,
                    encounter.PatientPid.Value, nowUtc),
                new AuditEntry(
                    nowUtc, AuditActor.Unknown, AuditActions.EncounterIssueUnlink, command.EncounterUuid, AuditOutcome.Success,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress)),
            ct);
    }
}

/// <summary>Lists the problems and allergies linked to one encounter, one bounded page at a time (API-6).</summary>
public sealed class ListEncounterIssuesUseCase(IEncounterReader encounters, IEncounterIssueReader reader)
{
    /// <exception cref="ClinicalCareValidationException">The page bounds are outside what is allowed.</exception>
    /// <exception cref="EncounterNotFoundException">No such encounter, or legacy deleted it.</exception>
    public async Task<EncounterIssuePage> ExecuteAsync(Guid encounterUuid, int? limit, int? offset, CancellationToken ct)
    {
        // Bounds first: a caller asking for 10,000 rows is told so without a database round trip.
        var page = PageRequest.Create(limit, offset);

        var encounter = await encounters.FindAsync(encounterUuid, ct)
            ?? throw new EncounterNotFoundException(encounterUuid);

        // An encounter with no number or no patient key cannot be linked to, so its list is empty.
        if (encounter.Number is not > 0 || encounter.PatientPid is not > 0)
            return new EncounterIssuePage([], 0);

        return await reader.ListForEncounterAsync(encounter.Number.Value, encounter.PatientPid.Value, page, ct);
    }
}
