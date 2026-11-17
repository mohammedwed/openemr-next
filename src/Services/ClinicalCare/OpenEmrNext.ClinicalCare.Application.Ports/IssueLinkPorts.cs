using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// The outbox payload for <c>IssueLinkedToEncounter</c>: ids and the issue's kind only (DOM-16, EVT-5), never its title or the resolved flag. The kind is a type name,
/// not clinical content.
/// </summary>
public sealed record IssueLinkedToEncounterEvent(
    Guid LinkUuid, Guid EncounterUuid, long EncounterNumber, Guid IssueUuid, string IssueKind, Guid PatientUuid, long PatientPid, DateTime LinkedAtUtc)
{
    public const string EventType = "IssueLinkedToEncounter";
}

/// <summary>The outbox payload for <c>IssueUnlinkedFromEncounter</c>: ids and the issue's kind only (DOM-16, EVT-5).</summary>
public sealed record IssueUnlinkedFromEncounterEvent(
    Guid EncounterUuid, long EncounterNumber, Guid IssueUuid, string IssueKind, Guid? PatientUuid, long PatientPid, DateTime UnlinkedAtUtc)
{
    public const string EventType = "IssueUnlinkedFromEncounter";
}

/// <summary>Finds an issue of a type this service owns, by its uuid. A medication, a surgery or any other type is not found (ADR 0070 #2).</summary>
public interface IIssueDirectory
{
    /// <returns>The issue, or <c>null</c> when none has this uuid or the row is not a problem or an allergy.</returns>
    Task<IssueReference?> FindAsync(Guid issueUuid, CancellationToken ct);
}

/// <summary>Hands a new link its identity (DOM-7).</summary>
public interface IEncounterIssueLinkIdentityAllocator
{
    EncounterIssueLinkIdentity Allocate();
}

/// <summary>How a link attempt ended: a new link, or the one that already existed (legacy's link is idempotent).</summary>
/// <param name="Created">Whether this call made the link.</param>
/// <param name="LinkUuid">The link's uuid, new or existing; <c>null</c> only for an existing legacy row that has none.</param>
/// <param name="Resolved">The stored resolved flag. For an existing link this is what is stored, not what was asked for: a link is never updated.</param>
public sealed record LinkRegistration(bool Created, Guid? LinkUuid, bool Resolved);

/// <summary>What one unlink must commit together (the event and the audit row), written only if a link was actually removed.</summary>
public sealed record UnlinkRequest(
    Guid EncounterUuid, long EncounterNumber, long PatientPid, IssueReference Issue, IssueUnlinkedFromEncounterEvent Event, AuditEntry Audit);

/// <summary>
/// What the link use cases need from persistence: a link committed with its <see cref="IssueLinkedToEncounterEvent"/> and its audit row, atomically, or an unlink
/// committed the same way (EVT-1, SEC-9). Both are idempotent, as legacy's are.
/// </summary>
public interface IEncounterIssueRegistry
{
    /// <summary>
    /// Makes the link if none exists for this patient, issue and encounter; otherwise returns the existing one and writes nothing. The event and audit row are written
    /// only when a link is created. If it throws, nothing was written.
    /// </summary>
    Task<LinkRegistration> LinkAsync(EncounterIssueLink link, IssueLinkedToEncounterEvent @event, AuditEntry audit, CancellationToken ct);

    /// <returns><c>true</c> if a link was removed (and its event and audit row written); <c>false</c> if there was none, in which case nothing was written.</returns>
    Task<bool> UnlinkAsync(UnlinkRequest request, CancellationToken ct);
}

/// <summary>Reads the issues linked to an encounter, without touching an aggregate (ADR 0054). Only problems and allergies are returned.</summary>
public interface IEncounterIssueReader
{
    /// <returns>One bounded page of the encounter's linked problems and allergies, with the total. A link whose issue no longer exists (legacy's REST delete leaves such orphans) is left out.</returns>
    Task<EncounterIssuePage> ListForEncounterAsync(long encounterNumber, long patientPid, PageRequest page, CancellationToken ct);
}
