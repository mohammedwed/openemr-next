using System.Text.Json;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>How an audited action ended (SEC-9).</summary>
public enum AuditOutcome
{
    /// <summary>The change was committed.</summary>
    Success = 0,

    /// <summary>The caller was not allowed to do it. Written only once a permission port exists (ADR 0069 #4).</summary>
    Denied = 1,

    /// <summary>The request reached the business layer and was refused or failed (validation, conflict, not found).</summary>
    Failed = 2
}

/// <summary>
/// Who did it. <paramref name="Verified"/> is <c>false</c> until the gateway forwards a validated session (ADR 0069 #5): until then
/// the subject is the provider id the caller <em>named</em>, and the row says so rather than pretending otherwise.
/// </summary>
/// <param name="Subject">The caller's stable identity (a user uuid once verified, <c>user:{providerId}</c> before).</param>
/// <param name="UserId">The legacy <c>users.id</c>, when known.</param>
/// <param name="Verified">Whether the subject came from a validated session.</param>
public sealed record AuditActor(string Subject, long? UserId, bool Verified);

/// <summary>The business verbs an audit row can carry (SEC-9: a verb, not a URL).</summary>
public static class AuditActions
{
    public const string EncounterCreate = "encounter.create";
    public const string EncounterUpdate = "encounter.update";
    public const string EncounterDelete = "encounter.delete";
    public const string VitalsRecord = "vitals.record";
    public const string VitalsCorrect = "vitals.correct";
    public const string VitalsDelete = "vitals.delete";
}

/// <summary>
/// One row of the audit trail (SEC-9, ADR 0069): what happened, to which record, by whom, with what result. Holds identifiers and
/// <em>field names</em> only, never a value, so a leaked audit table is not a leak of clinical data.
/// </summary>
/// <remarks>
/// Built from server-side constants and a clock, except <see cref="CorrelationId"/> and <see cref="SourceAddress"/>, which come from
/// request headers. Those two are cut to their column width instead of rejected: failing a clinical write because a header was long
/// would be worse than a shortened audit field (the lesson of review findings R-1 to R-7).
/// </remarks>
public sealed class AuditEntry
{
    /// <summary>Column widths in <c>clinical_care_audit</c>; the seed script is the other place these numbers live.</summary>
    public const int MaxActorLength = 100, MaxActionLength = 50, MaxCorrelationLength = 100, MaxAddressLength = 45, MaxDetailsLength = 1000;

    public AuditEntry(
        DateTime atUtc, AuditActor actor, string action, Guid subject, AuditOutcome outcome,
        string? correlationId = null, string? sourceAddress = null, IReadOnlyList<string>? changedFields = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        if (action.Length > MaxActionLength)
            throw new ArgumentException($"An audit action is at most {MaxActionLength} characters.", nameof(action));
        // A success names the record it changed. A refusal names whatever the caller asked about, which may not exist and may be
        // the empty uuid: that is still worth a row, and must not turn a 400 or 404 into a 500.
        if (subject == Guid.Empty && outcome == AuditOutcome.Success)
            throw new ArgumentException("A successful change names the record it changed.", nameof(subject));

        AtUtc = atUtc;
        Actor = actor with { Subject = Cut(actor.Subject, MaxActorLength) ?? "unknown" };
        Action = action;
        Subject = subject;
        Outcome = outcome;
        CorrelationId = Cut(correlationId, MaxCorrelationLength);
        SourceAddress = Cut(sourceAddress, MaxAddressLength);
        ChangedFields = changedFields ?? [];

        // Field names come from code, so an oversized list is a programming error, not a data problem.
        if (Details is { Length: > MaxDetailsLength })
            throw new ArgumentException($"The changed-field list serializes to more than {MaxDetailsLength} characters.", nameof(changedFields));
    }

    public DateTime AtUtc { get; }
    public AuditActor Actor { get; }
    public string Action { get; }
    public Guid Subject { get; }
    public AuditOutcome Outcome { get; }
    public string? CorrelationId { get; }
    public string? SourceAddress { get; }

    /// <summary>The names of the fields a change touched (never their values). Empty for actions with no field list.</summary>
    public IReadOnlyList<string> ChangedFields { get; }

    /// <summary>The stored form of <see cref="ChangedFields"/>: a JSON array, or <c>null</c> when there are none.</summary>
    public string? Details => ChangedFields.Count == 0 ? null : JsonSerializer.Serialize(ChangedFields);

    /// <summary>The stored, lower-case name of <see cref="Outcome"/>.</summary>
    public string OutcomeName => Outcome switch
    {
        AuditOutcome.Success => "success",
        AuditOutcome.Denied => "denied",
        AuditOutcome.Failed => "failed",
        _ => throw new InvalidOperationException($"Unknown audit outcome {Outcome}.")
    };

    private static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
