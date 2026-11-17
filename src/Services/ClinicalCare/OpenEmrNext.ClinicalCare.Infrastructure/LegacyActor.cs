using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// "Who did it", the way legacy records it on the rows it writes: a username and a group (<c>forms.user</c>,
/// <c>forms.groupname</c>, <c>form_vitals.user</c>, <c>form_vitals.groupname</c>). Legacy takes both from the signed-in session; this
/// service has only the provider id the caller named (finding F-CC-1), so it looks both up from it. One place, so the lookup and
/// the fallback are written once (review finding R-12; it had been copied three times, each with its own <c>'Default'</c>).
/// </summary>
/// <param name="Username">The provider's <c>users.username</c>, or <c>null</c> when no such user exists (finding F-CC-6: this adapter does not invent one).</param>
/// <param name="GroupName">The user's <c>groups</c> entry, or <see cref="DefaultGroup"/>.</param>
internal sealed record LegacyActor(string? Username, string GroupName)
{
    /// <summary>
    /// The group legacy itself writes in one path that has no session (<c>CdaTemplateImportDispose.php</c>, 8.0.0). Not verified
    /// against a signed-in session's <c>authProvider</c> for every user (slice 1 doc, section 8).
    /// </summary>
    public const string DefaultGroup = "Default";

    /// <summary>Resolves the actor for <paramref name="providerId"/>. Two plain, parameterized reads; run it inside the caller's transaction.</summary>
    public static async Task<LegacyActor> ResolveAsync(ClinicalCareDbContext db, long providerId, CancellationToken ct)
    {
        var usernames = await db.Database
            .SqlQuery<string?>($"SELECT username AS Value FROM users WHERE id = {providerId}")
            .ToListAsync(ct);
        var username = usernames.FirstOrDefault();

        if (string.IsNullOrEmpty(username))
            return new LegacyActor(null, DefaultGroup);

        var groups = await db.Database
            .SqlQuery<string?>($"SELECT name AS Value FROM `groups` WHERE `user` = {username} LIMIT 1")
            .ToListAsync(ct);

        return new LegacyActor(username, groups.FirstOrDefault() is { Length: > 0 } group ? group : DefaultGroup);
    }
}
