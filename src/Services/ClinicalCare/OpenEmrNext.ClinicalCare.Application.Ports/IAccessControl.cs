namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// A right legacy's access control grants or refuses, named the way legacy names it: an access-control object section and value, for example <c>admin</c> and <c>super</c>. The names are legacy's own because the
/// rules that grant them are legacy's data (ADR 0076); this service does not invent a second set.
/// </summary>
public readonly record struct AccessRight(string Section, string Value)
{
    /// <summary>Legacy's superuser right. Whoever holds it holds every other right too, as in legacy.</summary>
    public static readonly AccessRight AdminSuper = new("admin", "super");

    /// <summary>The right legacy requires to delete a form from an encounter (vitals, SOAP notes, clinical notes) or an issue.</summary>
    public static readonly AccessRight DeleteRecords = AdminSuper;
}

/// <summary>
/// Asks legacy's access control whether a user may do something. A port so the coupling to legacy's <c>gacl_*</c> tables stays in one adapter, and so the use cases that need a decision can be tested with a fake that says
/// yes or no (ADR 0076).
/// </summary>
public interface IAccessControl
{
    /// <summary>
    /// Whether <paramref name="username"/> holds <paramref name="right"/>, with legacy's own answer: some enabled rule allows it to the user or to a group the user belongs to (or inherits from), no enabled rule refuses
    /// it, and a superuser holds every right.
    /// </summary>
    /// <returns><c>false</c> for a user with no rule, an unknown user, or an empty username: refusal is the answer whenever legacy's rules do not say yes.</returns>
    Task<bool> IsAllowedAsync(string? username, AccessRight right, CancellationToken ct);
}
