using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Sets a patient's two advance-directive status flags -- a direct pass-through to the port, with no
/// Domain validation of its own: <paramref name="status"/> is already a real
/// <see cref="AdvanceDirectiveCompletionStatus"/> by the time it reaches here (the Api layer rejects
/// an unparseable wire value before this runs), and legacy defines no rule linking
/// <paramref name="authenticatorUserId"/>'s validity to <paramref name="status"/>'s value for this
/// service to enforce.
/// </summary>
public sealed class SetAdvanceDirectiveStatusUseCase(IAdvanceDirectiveRepository repository)
{
    public Task ExecuteAsync(
        long pid, AdvanceDirectiveCompletionStatus status, DateTime? reviewedAtUtc,
        long? authenticatorUserId, CancellationToken ct) =>
        repository.SetStatusAsync(pid, status, reviewedAtUtc, authenticatorUserId, ct);
}
