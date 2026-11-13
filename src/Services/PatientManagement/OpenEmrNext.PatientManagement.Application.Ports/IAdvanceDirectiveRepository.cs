using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// What the advance-directive use cases need from persistence: the uploaded-document side (legacy's
/// <c>documents</c> + <c>categories_to_documents</c>, ADR 0049 -- no new table) and the status-flag side
/// (<c>patient_data.completed_ad</c>/<c>ad_reviewed</c>/<c>advance_directive_user_authenticator</c>),
/// kept on one port because both halves of legacy's own <c>PatientAdvanceDirectiveService</c>/
/// <c>advancedirectives.php</c> feature, not two independent aggregates.
/// </summary>
public interface IAdvanceDirectiveRepository
{
    Task AddDocumentAsync(AdvanceDirectiveDocument document, CancellationToken ct);

    Task<IReadOnlyList<AdvanceDirectiveDocumentSummary>> GetDocumentsAsync(long pid, CancellationToken ct);

    /// <summary>The raw file bytes for one document, or <c>null</c> if no document with this uuid
    /// exists for this patient.</summary>
    Task<(string MimeType, string FileName, byte[] Content)?> GetDocumentContentAsync(long pid, Guid uuid, CancellationToken ct);

    Task SetStatusAsync(
        long pid, AdvanceDirectiveCompletionStatus status, DateTime? reviewedAtUtc,
        long? authenticatorUserId, CancellationToken ct);

    Task<(AdvanceDirectiveCompletionStatus Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId)> GetStatusAsync(
        long pid, CancellationToken ct);
}
