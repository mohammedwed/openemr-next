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

    public async Task<IReadOnlyDictionary<long, Guid>> FindUuidsAsync(IReadOnlyCollection<long> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(i => i > 0).Distinct().Take(500).ToList();
        if (ids.Count == 0)
            return new Dictionary<long, Guid>();

        // One bound parameter per id (EF cannot bind a list to IN): the placeholders are built from the count, never from a value.
        var placeholders = string.Join(',', ids.Select((_, i) => "{" + i + "}"));
#pragma warning disable EF1003 // The text is built from a count of placeholders and nothing else; every id travels as a bound parameter.
        var rows = await db.Database
            .SqlQueryRaw<UuidRow>("SELECT id AS Id, uuid AS Uuid FROM users WHERE uuid IS NOT NULL AND id IN (" + placeholders + ")", ids.Cast<object>().ToArray())
            .ToListAsync(ct);
#pragma warning restore EF1003

        return rows.ToDictionary(r => r.Id, r => Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid));
    }

    public async Task<IReadOnlyDictionary<long, UserPerson>> FindPeopleAsync(IReadOnlyCollection<long> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(i => i > 0).Distinct().Take(500).ToList();
        if (ids.Count == 0)
            return new Dictionary<long, UserPerson>();

        var placeholders = string.Join(',', ids.Select((_, i) => "{" + i + "}"));
#pragma warning disable EF1003 // The text is built from a count of placeholders and nothing else; every id travels as a bound parameter.
        var rows = await db.Database
            .SqlQueryRaw<PersonRow>("SELECT id AS Id, uuid AS Uuid, fname AS FirstName, lname AS LastName FROM users WHERE uuid IS NOT NULL AND id IN (" + placeholders + ")", ids.Cast<object>().ToArray())
            .ToListAsync(ct);
#pragma warning restore EF1003

        return rows.ToDictionary(
            r => r.Id,
            r => new UserPerson(Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid), string.Join(' ', new[] { r.FirstName, r.LastName }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim())) is { Length: > 0 } n ? n : null));
    }

    private sealed class PersonRow
    {
        public long Id { get; set; }
        public byte[] Uuid { get; set; } = [];
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    private sealed class UuidRow
    {
        public long Id { get; set; }
        public byte[] Uuid { get; set; } = [];
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public string? Username { get; set; }
        public long Active { get; set; }
    }
}
