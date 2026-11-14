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

        // The whole unit of work runs through an execution strategy, not a bare BeginTransactionAsync/
        // CommitAsync, because the DbContext has EnableRetryOnFailure on -- a user-initiated transaction
        // has to be replayable wholesale for that to work. AssignGeneratedIdentity stays outside the
        // delegate and runs exactly once, after a successful commit: it throws if called twice, and a
        // retried attempt re-executes the whole delegate (including a fresh id) from scratch.
        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            // A transaction pins the connection for both statements below -- see
            // DatabaseFacadeExtensions.GetLastInsertIdAsync for why that's required -- the same way
            // PersistSwapAsync below already needs one anyway.
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

            var generatedId = await db.Database.GetLastInsertIdAsync(ct);
            await transaction.CommitAsync(ct);
            return generatedId;
        });

        policy.AssignGeneratedIdentity(id);
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

    /// <summary>
    /// Builds the typed cluster <see cref="InsuranceCoverage"/> loads from. A pre-existing legacy row
    /// can hold a <c>type</c> this service doesn't recognize, or a zero/partial date (<c>0000-00-00</c>,
    /// <c>2026-05-00</c>) that isn't a real <see cref="DateOnly"/> at all -- such a row is skipped, not
    /// thrown on, so one malformed row elsewhere on a patient's record never breaks every operation on
    /// their coverage (adding a policy included, not just the swap path this method originally existed
    /// for). Losing nothing a caller could have observed either way: a row that can't become a typed
    /// entity could never collide with a new policy's own real start date, or be a valid swap target,
    /// under this service's own rules.
    /// </summary>
    public async Task<IReadOnlyList<InsurancePolicy>> GetPoliciesAsync(long pid, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<RawTypedRow>(
            """
            SELECT id AS Id, uuid AS Uuid, type AS Type, provider AS Provider,
                   policy_number AS PolicyNumber, group_number AS GroupNumber, plan_name AS PlanName,
                   CAST(date AS CHAR(10)) AS Date, CAST(date_end AS CHAR(10)) AS DateEnd
            FROM insurance_data
            WHERE pid = {0}
            """, pid).ToListAsync(ct);

        var policies = new List<InsurancePolicy>();
        foreach (var r in rows)
        {
            if (InsuranceLevelExtensions.TryParse(r.Type) is not { } level)
                continue;

            if (TryParseDateOnly(r.Date) is not { } startDate)
                continue;

            policies.Add(InsurancePolicy.Reconstitute(
                r.Id, Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid), pid, level,
                long.TryParse(r.Provider, out var companyId) ? companyId : 0,
                r.PolicyNumber ?? "", r.GroupNumber, r.PlanName,
                startDate, TryParseDateOnly(r.DateEnd)));
        }

        return policies;
    }

    private static DateOnly? TryParseDateOnly(string? stored) =>
        DateOnly.TryParseExact(stored, "yyyy-MM-dd", out var day) ? day : null;

    public async Task PersistSwapAsync(InsuranceSwapResult result, CancellationToken ct)
    {
        var source = result.Source;
        var target = result.DisplacedOccupant;

        // Same execution-strategy requirement as AddAsync above -- EnableRetryOnFailure needs a
        // user-initiated transaction wrapped so the whole unit of work can be replayed, not just
        // begun/committed directly. Unlike AddAsync, nothing here mutates an external aggregate, so a
        // retried attempt re-running the same UPDATEs is naturally safe -- they just reapply the same
        // already-decided target values.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                if (target is not null)
                {
                    // Same start date as the source -> the (pid, type, date) unique constraint would
                    // collide the instant the source's type changes to the target's old type, before
                    // the target's own type changes away from it. Legacy's own workaround: park the
                    // target on a date so far out it will never collide with anything real, write both
                    // rows' new types in that state, then restore the target's real date -- all inside
                    // this one transaction, so a failure anywhere rolls every bit of it back. A pure
                    // persistence detail: the swap decision itself (InsuranceCoverage.Swap) neither
                    // knows nor cares about this constraint.
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
        });
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
        public string? Date { get; set; }
        public string? DateEnd { get; set; }
    }
}
