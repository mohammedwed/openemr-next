using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IInsurancePolicyRepository"/>. Raw SQL throughout, not an EF-mapped
/// entity like <see cref="PatientManagementDbContext.Patients"/>: <see cref="GetRowsAsync"/> has to
/// read <c>date</c>/<c>date_end</c> as the literal stored text (<c>CAST(... AS CHAR)</c>, forcing the
/// server to render them as text before EF's own date parsing ever sees them), since a pre-existing
/// row can hold a zero or partial date that isn't a real <see cref="DateOnly"/> at all -- mapping
/// this table through EF's normal typed columns would throw on exactly the rows
/// <see cref="EffectiveInsuranceRule"/> exists to tolerate. One mapping strategy for the whole table,
/// not two, so writes use the same raw-SQL style as reads.
/// </summary>
public sealed class EfInsurancePolicyRepository(PatientManagementDbContext db) : IInsurancePolicyRepository
{
    public async Task AddAsync(InsurancePolicy policy, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(policy.Uuid);

        // LAST_INSERT_ID() is scoped to the connection session, not the statement -- without an
        // explicit transaction here, EF Core is free to run the INSERT and the follow-up SELECT on
        // two different pooled connections, reading back someone else's id or none at all (caught
        // the hard way: against a real database, not by reasoning about it). A transaction pins the
        // connection for both statements, the same way SwapAsync below already needs one anyway.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO insurance_data
                (pid, type, provider, policy_number, group_number, plan_name, date, date_end, uuid)
            VALUES
                ({policy.Pid}, {policy.Level.ToWireValue()}, {policy.CompanyId.ToString()}, {policy.PolicyNumber},
                 {policy.GroupNumber}, {policy.PlanName}, {policy.StartDate.ToString("yyyy-MM-dd")},
                 {policy.EndDate?.ToString("yyyy-MM-dd")}, {uuidBytes})
            """, ct);

        var id = await db.Database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
        policy.AssignGeneratedIdentity(id);

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<InsurancePolicyRow>> GetRowsAsync(long pid, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<RawInsuranceRow>(
            """
            SELECT id AS Id, uuid AS Uuid, type AS Type, provider AS Provider,
                   CAST(date AS CHAR(10)) AS Date, CAST(date_end AS CHAR(10)) AS DateEnd
            FROM insurance_data
            WHERE pid = {0}
            """, pid).ToListAsync(ct);

        return [.. rows.Select(r => new InsurancePolicyRow(
            r.Id,
            r.Uuid is { Length: 16 } bytes ? Rfc4122GuidConverter.FromRfc4122Bytes(bytes) : null,
            r.Type,
            r.Provider,
            r.Date ?? "",
            r.DateEnd ?? ""))];
    }

    public async Task SwapAsync(long pid, InsuranceLevel targetLevel, Guid policyUuid, CancellationToken ct)
    {
        var targetType = targetLevel.ToWireValue();
        var sourceUuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(policyUuid);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Both rows' current state, read up front -- everything below only ever writes, using
            // these captured values, never re-reading mid-swap (legacy's own shape: it loads both
            // full rows once, then issues writes from the in-memory copies).
            var source = await db.Database.SqlQueryRaw<RawOccupant>(
                "SELECT uuid AS Uuid, type AS Type, date AS Date FROM insurance_data WHERE uuid = {0}",
                sourceUuidBytes).SingleAsync(ct);

            // The row (if any) currently occupying the target level -- it trades places with the
            // source policy. "ORDER BY (date IS NULL) ASC, date DESC" matches legacy exactly: a real
            // start date outranks a blank one for "which row currently holds this level" purposes too.
            var target = await db.Database.SqlQueryRaw<RawOccupant>(
                """
                SELECT uuid AS Uuid, type AS Type, date AS Date
                FROM insurance_data
                WHERE pid = {0} AND type = {1}
                ORDER BY (date IS NULL) ASC, date DESC
                LIMIT 1
                """, pid, targetType).FirstOrDefaultAsync(ct);

            if (target is not null)
            {
                // Same date as the source -> the (pid, type, date) unique constraint would collide
                // the instant the source's type changes to targetType, before target's own type
                // changes away from it. Legacy's own workaround: park target on a date so far out it
                // will never collide with anything real, swap both rows' types in that state, then
                // restore target's real date -- all inside this one transaction, so a failure
                // anywhere rolls every bit of it back.
                var collides = target.Date == source.Date;
                var targetDateDuringSwap = collides ? DateTime.Parse("9999-12-31") : target.Date;

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE insurance_data SET type = {source.Type}, date = {targetDateDuringSwap} WHERE uuid = {target.Uuid}", ct);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE insurance_data SET type = {targetType} WHERE uuid = {sourceUuidBytes}", ct);

                if (collides)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE insurance_data SET date = {target.Date} WHERE uuid = {target.Uuid}", ct);
                }
            }
            else
            {
                // The target level was empty -- nothing to trade places with, just move the source.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE insurance_data SET type = {targetType} WHERE uuid = {sourceUuidBytes}", ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private sealed class RawInsuranceRow
    {
        public long Id { get; set; }
        public byte[]? Uuid { get; set; }
        public string? Type { get; set; }
        public string? Provider { get; set; }
        public string? Date { get; set; }
        public string? DateEnd { get; set; }
    }

    private sealed class RawOccupant
    {
        public byte[] Uuid { get; set; } = [];
        public string Type { get; set; } = "";
        public DateTime? Date { get; set; }
    }
}
