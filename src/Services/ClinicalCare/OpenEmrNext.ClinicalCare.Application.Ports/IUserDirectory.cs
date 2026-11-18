namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>A person a FHIR resource refers to: their OpenEMR uuid, and the name to show (first and last), when legacy holds one.</summary>
public sealed record UserPerson(Guid Uuid, string? DisplayName);

/// <summary>A legacy user, as far as Clinical Care needs one: the numeric key legacy's columns hold, their username, and whether the account is active.</summary>
public sealed record UserRecord(long Id, string? Username, bool Active);

/// <summary>
/// Resolves the OpenEMR user uuid the gateway verified (the session's subject, ADR 0075) to the <c>users.id</c> and username legacy's own columns record. A port so the coupling to legacy's <c>users</c> table
/// stays in one adapter.
/// </summary>
public interface IUserDirectory
{
    /// <returns>The user, or <c>null</c> when no user has this uuid.</returns>
    Task<UserRecord?> FindByUuidAsync(Guid uuid, CancellationToken ct);

    /// <summary>The OpenEMR uuid of each of these users, for the ones that have one: what a FHIR reference to a person is built from (ADR 0079 #7). A user with no uuid, or no row, is simply absent.</summary>
    Task<IReadOnlyDictionary<long, Guid>> FindUuidsAsync(IReadOnlyCollection<long> userIds, CancellationToken ct);

    /// <summary>As <see cref="FindUuidsAsync"/>, with each person's display name too (the users grant allows first and last name, ADR 0075 amendment): what a reference with a name is built from (ADR 0079 #7).</summary>
    Task<IReadOnlyDictionary<long, UserPerson>> FindPeopleAsync(IReadOnlyCollection<long> userIds, CancellationToken ct);
}
