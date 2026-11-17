using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IUserDirectory"/>: one parameterized read of <c>users</c> by <c>uuid</c>, for the three columns the service is granted (<c>id</c>, <c>uuid</c>, <c>active</c>): none of the credential or contact
/// columns (finding F-CC-7). An account legacy has deactivated reads as inactive and is refused by the API.
/// </summary>
public sealed class EfUserDirectory(PatientManagementDbContext db) : IUserDirectory
{
    public async Task<UserRecord?> FindByUuidAsync(Guid uuid, CancellationToken ct)
    {
        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);
        var rows = await db.Database
            .SqlQueryRaw<UserRow>("SELECT id AS Id, CAST(active AS SIGNED) AS Active FROM users WHERE uuid = {0} LIMIT 1", bytes)
            .ToListAsync(ct);

        return rows.Select(r => new UserRecord(r.Id, r.Active != 0)).FirstOrDefault();
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public long Active { get; set; }
    }
}
