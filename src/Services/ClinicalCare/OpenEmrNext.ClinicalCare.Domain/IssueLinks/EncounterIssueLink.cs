using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.IssueLinks;

/// <summary>
/// The identity a new link is given: its uuid, handed in rather than invented inside the domain (DOM-7). Legacy's <c>issue_encounter</c> row has a uuid of its own.
/// </summary>
public sealed record EncounterIssueLinkIdentity
{
    public Guid Uuid { get; }

    private EncounterIssueLinkIdentity(Guid uuid) => Uuid = uuid;

    public static EncounterIssueLinkIdentity Create(Guid uuid) =>
        uuid == Guid.Empty
            ? throw new ClinicalCareValidationException(["The link identity must have a uuid."])
            : new EncounterIssueLinkIdentity(uuid);
}

/// <summary>
/// The link between an encounter and an issue (a problem or an allergy) it addressed: one row of legacy's <c>issue_encounter</c>, unique on patient, issue and encounter
/// (M5c slice 4, ADR 0070). <see cref="Create"/> is the only way to get one.
/// </summary>
/// <remarks>
/// Rule IE-3, which legacy does not have: the issue and the encounter must be the <b>same patient's</b>. Legacy's <c>linkIssueToEncounter</c> takes a patient id and trusts it, so a
/// link between two patients' records is possible there; here it is refused, because a clinical record that names another patient's problem is a safety defect, not a quirk.
/// <para>
/// A link has no update: legacy has none either. Changing <see cref="Resolved"/> (whether the issue was resolved during that encounter) is an unlink and a link.
/// </para>
/// </remarks>
public sealed class EncounterIssueLink
{
    /// <summary>
    /// <c>issue_encounter.encounter</c> and <c>issue_encounter.list_id</c> are signed <c>int(11)</c>, but the numbers they hold come from wider sources: an encounter number is
    /// drawn from <c>sequences.id</c> (an <i>unsigned</i> int, up to 4,294,967,295) and stored in <c>form_encounter.encounter</c> (a bigint), and <c>lists.id</c> is a bigint. A
    /// value above this is a real encounter or issue that a link cannot name. It must be refused: under <c>INSERT IGNORE</c> the database would clamp it to this maximum and write
    /// a link to a <b>different</b> encounter (review finding R-15).
    /// </summary>
    public const long MaxStorableId = int.MaxValue;

    public long Id { get; private set; }
    public Guid Uuid { get; }
    public Guid EncounterUuid { get; }
    public long EncounterNumber { get; }
    public PatientReference Patient { get; }
    public IssueReference Issue { get; }

    /// <summary>Whether the issue was resolved during this encounter (<c>issue_encounter.resolved</c>).</summary>
    public bool Resolved { get; }

    private EncounterIssueLink(
        Guid encounterUuid, long encounterNumber, PatientReference patient, IssueReference issue, bool resolved, EncounterIssueLinkIdentity identity)
    {
        Uuid = identity.Uuid;
        EncounterUuid = encounterUuid;
        EncounterNumber = encounterNumber;
        Patient = patient;
        Issue = issue;
        Resolved = resolved;
    }

    /// <param name="encounterUuid">The encounter's public identity.</param>
    /// <param name="encounterNumber">Legacy's encounter number, which the link row stores. A legacy-created encounter can lack one.</param>
    /// <param name="patientUuid">The encounter's patient, or <c>null</c> when its pid matches no known patient.</param>
    /// <param name="patientPid">The encounter's patient by legacy key.</param>
    /// <param name="issue">The issue to link, read beforehand by the use case.</param>
    /// <param name="resolved">Whether the issue was resolved during the encounter.</param>
    /// <exception cref="ClinicalCareValidationException">The encounter has no number or no known patient, or the issue belongs to a different patient.</exception>
    public static EncounterIssueLink Create(
        Guid encounterUuid, long? encounterNumber, Guid? patientUuid, long? patientPid, IssueReference issue, bool resolved,
        EncounterIssueLinkIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var errors = new List<string>();

        if (encounterNumber is not > 0)
            errors.Add("The encounter has no encounter number, so an issue cannot be linked to it.");
        else if (encounterNumber > MaxStorableId)
            errors.Add($"The encounter's number is too large for a link to store (legacy's issue_encounter holds numbers up to {MaxStorableId}).");

        // issue_encounter.list_id is the same signed 32-bit column, and lists.id is a bigint: a row id beyond it could not be stored.
        if (issue.Id > MaxStorableId)
            errors.Add($"The issue's row id is too large for a link to store (legacy's issue_encounter holds ids up to {MaxStorableId}).");

        if (patientUuid is null || patientUuid == Guid.Empty || patientPid is not > 0)
            errors.Add("The encounter's patient is unknown, so an issue cannot be linked to it.");
        else if (issue.Pid != patientPid)
            errors.Add("The issue belongs to a different patient than the encounter.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new EncounterIssueLink(
            encounterUuid, encounterNumber!.Value, PatientReference.Create(patientUuid!.Value, patientPid!.Value), issue, resolved, identity);
    }

    /// <summary>Called only by the Infrastructure adapter after the insert, to record the database-assigned key (the narrow, internal exception, DOM-5).</summary>
    internal void AssignGeneratedId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("The link's database id has already been assigned.");

        Id = id;
    }
}
