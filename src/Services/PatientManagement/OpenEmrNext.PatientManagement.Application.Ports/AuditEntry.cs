using System.Text.Json;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>How an audited action ended. The names are what the table stores.</summary>
public enum AuditOutcome { Success = 0, Failed = 2 }

/// <summary>
/// Who acted. <paramref name="Subject"/> is the user's OpenEMR uuid as text when the gateway verified them (ADR 0075), <c>unknown</c> otherwise; <paramref name="UserId"/> is legacy's <c>users.id</c>.
/// </summary>
public sealed record AuditActor(string Subject, long? UserId, bool Verified)
{
    public static readonly AuditActor Unknown = new("unknown", null, Verified: false);
}

/// <summary>Where a request came from: the actor, the trace id that links it to the logs, and the caller's address. Handed to a use case so the audit row it writes says who and from where.</summary>
public sealed record AuditOrigin(AuditActor Actor, string? CorrelationId, string? SourceAddress)
{
    public static readonly AuditOrigin Unknown = new(AuditActor.Unknown, null, null);
}

/// <summary>The actions Patient Management audits, as the table stores them.</summary>
public static class AuditActions
{
    public const string PatientRegister = "patient.register";
    public const string PatientUpdate = "patient.update";
    public const string InsuranceAdd = "insurance.add";
    public const string InsuranceSwap = "insurance.swap";
    public const string DirectiveDocumentUpload = "directive.document.upload";
    public const string DirectiveStatusSet = "directive.status.set";
}

/// <summary>
/// One row of the audit trail (SEC-9; the same shape as Clinical Care's, ADR 0069): what happened, to which record, by whom, with what result. Holds identifiers and <em>field names</em> only, never a value, so a
/// leaked audit table is not a leak of patient data. Built from server-side constants, a clock and the request's origin; the two header-derived values are cut to their column width, not rejected, because failing
/// a registration over a long header would be worse than a shortened audit field.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Column widths in <c>patient_management_audit</c>; the seed script is the other place these numbers live.</summary>
    public const int MaxActorLength = 100, MaxActionLength = 50, MaxCorrelationLength = 100, MaxAddressLength = 45, MaxDetailsLength = 1000, MaxSubjectLength = 36;

    /// <param name="subject">The record's identifier as text: a uuid, or a short key such as <c>patient:42</c> for a patient addressed by its number. Cut to the column width.</param>
    public AuditEntry(
        DateTime atUtc, AuditActor actor, string action, string subject, AuditOutcome outcome,
        string? correlationId = null, string? sourceAddress = null, IReadOnlyList<string>? changedFields = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (action.Length > MaxActionLength)
            throw new ArgumentException($"An audit action is at most {MaxActionLength} characters.", nameof(action));

        AtUtc = atUtc;
        Actor = actor with { Subject = Cut(actor.Subject, MaxActorLength) ?? "unknown" };
        Action = action;
        Subject = Cut(subject, MaxSubjectLength)!;
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
    public string Subject { get; }
    public AuditOutcome Outcome { get; }
    public string? CorrelationId { get; }
    public string? SourceAddress { get; }
    public IReadOnlyList<string> ChangedFields { get; }

    /// <summary>The field names as a JSON array, or <c>null</c> when there are none.</summary>
    public string? Details => ChangedFields.Count == 0 ? null : JsonSerializer.Serialize(ChangedFields);

    public string OutcomeName => Outcome switch
    {
        AuditOutcome.Success => "success",
        AuditOutcome.Failed => "failed",
        _ => throw new InvalidOperationException($"Unknown audit outcome {Outcome}.")
    };

    private static string? Cut(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
