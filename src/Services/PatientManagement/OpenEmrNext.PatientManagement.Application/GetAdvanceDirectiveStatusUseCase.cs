using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Reads back a patient's two advance-directive status flags
/// (<c>patient_data.completed_ad</c>/<c>ad_reviewed</c>/<c>advance_directive_user_authenticator</c>)
/// -- independent legacy facts from the uploaded-document side
/// (<see cref="GetAdvanceDirectiveDocumentsUseCase"/>), not reconciled with it by any logic here or
/// in legacy itself: a patient can have a completed status with zero documents, or the reverse.
/// </summary>
public sealed class GetAdvanceDirectiveStatusUseCase(IAdvanceDirectiveRepository repository)
{
    public Task<(AdvanceDirectiveCompletionStatus Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId)> ExecuteAsync(
        long pid, CancellationToken ct) =>
        repository.GetStatusAsync(pid, ct);
}
