using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

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

    public async Task<IReadOnlyList<InsurancePolicy>> GetPoliciesAsync(long pid, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<RawTypedRow>(
            """
            SELECT id AS Id, uuid AS Uuid, type AS Type, provider AS Provider,
                   policy_number AS PolicyNumber, group_number AS GroupNumber, plan_name AS PlanName,
                   date AS Date, date_end AS DateEnd
            FROM insurance_data
            WHERE pid = {0}
            """, pid).ToListAsync(ct);

        return [.. rows.Select(r => InsurancePolicy.Reconstitute(
            r.Id, Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid), pid,
            InsuranceLevelExtensions.TryParse(r.Type) ?? throw new InvalidOperationException($"Unknown insurance level '{r.Type}' on policy {r.Id}."),
            long.TryParse(r.Provider, out var companyId) ? companyId : 0,
            r.PolicyNumber ?? "", r.GroupNumber, r.PlanName,
            DateOnly.FromDateTime(r.Date), r.DateEnd is { } end ? DateOnly.FromDateTime(end) : null))];
    }

    public async Task PersistSwapAsync(InsuranceSwapResult result, CancellationToken ct)
    {
        var source = result.Source;
        var target = result.DisplacedOccupant;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (target is not null)
            {
                // Same start date as the source -> the (pid, type, date) unique constraint would
                // collide the instant the source's type changes to the target's old type, before the
                // target's own type changes away from it. Legacy's own workaround: park the target on
                // a date so far out it will never collide with anything real, write both rows' new
                // types in that state, then restore the target's real date -- all inside this one
                // transaction, so a failure anywhere rolls every bit of it back. A pure persistence
                // detail: the swap decision itself (InsuranceCoverage.Swap) neither knows nor cares
                // about this constraint.
                var collides = target.StartDate == source.StartDate;
                var targetDateDuringSwap = collides
                    ? new DateOnly(9999, 12, 31).ToString("yyyy-MM-dd")
                    : target.StartDate.ToString("yyyy-MM-dd");

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE insurance_data SET type = {target.Level.ToWireValue()}, date = {targetDateDuringSwap}
                    WHERE uuid = {Rfc4122GuidConverter.ToRfc4122Bytes(target.Uuid)}
                    """, ct);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE insurance_data SET type = {source.Level.ToWireValue()}
                    WHERE uuid = {Rfc4122GuidConverter.ToRfc4122Bytes(source.Uuid)}
                    """, ct);

                if (collides)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE insurance_data SET date = {target.StartDate.ToString("yyyy-MM-dd")}
                        WHERE uuid = {Rfc4122GuidConverter.ToRfc4122Bytes(target.Uuid)}
                        """, ct);
                }
            }
            else
            {
                // The target level was empty -- nothing to trade places with, just move the source.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE insurance_data SET type = {source.Level.ToWireValue()}
                    WHERE uuid = {Rfc4122GuidConverter.ToRfc4122Bytes(source.Uuid)}
                    """, ct);
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

    private sealed class RawTypedRow
    {
        public long Id { get; set; }
        public byte[] Uuid { get; set; } = [];
        public string? Type { get; set; }
        public string? Provider { get; set; }
        public string? PolicyNumber { get; set; }
        public string? GroupNumber { get; set; }
        public string? PlanName { get; set; }
        public DateTime Date { get; set; }
        public DateTime? DateEnd { get; set; }
    }
}
