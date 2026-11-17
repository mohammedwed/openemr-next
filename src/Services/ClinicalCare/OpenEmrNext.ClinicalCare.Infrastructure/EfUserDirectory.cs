using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IUserDirectory"/>: one parameterized read of <c>users</c> by <c>uuid</c>, for the four columns the service is granted (<c>id</c>, <c>username</c>, <c>uuid</c>, <c>active</c>): none of
/// the credential or contact columns (ADR 0064). An account legacy has deactivated reads as inactive and is refused by the API.
/// </summary>
public sealed class EfUserDirectory(ClinicalCareDbContext db) : IUserDirectory
{
    public async Task<UserRecord?> FindByUuidAsync(Guid uuid, CancellationToken ct)
    {
        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var rows = await db.Database
            .SqlQuery<UserRow>($"SELECT id AS Id, username AS Username, CAST(active AS SIGNED) AS Active FROM users WHERE uuid = {bytes} LIMIT 1")
            .ToListAsync(ct);

        return rows.Select(r => new UserRecord(r.Id, string.IsNullOrWhiteSpace(r.Username) ? null : r.Username, r.Active != 0)).FirstOrDefault();
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public string? Username { get; set; }
        public long Active { get; set; }
    }
}
