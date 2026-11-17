namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>A legacy user, as far as Patient Management needs one: the numeric key legacy's columns hold, and whether the account is active.</summary>
public sealed record UserRecord(long Id, bool Active);

/// <summary>
/// Resolves the OpenEMR user uuid the gateway verified (the session's subject, ADR 0075) to the <c>users.id</c> that <c>patient_data.created_by</c>, <c>documents.owner</c> and the advance-directive reviewer columns hold.
/// A port so the coupling to legacy's <c>users</c> table stays in one adapter.
/// </summary>
public interface IUserDirectory
{
    /// <returns>The user, or <c>null</c> when no user has this uuid.</returns>
    Task<UserRecord?> FindByUuidAsync(Guid uuid, CancellationToken ct);
}
