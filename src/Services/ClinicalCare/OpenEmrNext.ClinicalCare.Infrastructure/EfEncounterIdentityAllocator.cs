using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterIdentityAllocator"/>: draws the encounter number from legacy's one
/// shared <c>sequences</c> counter (CC-5), the same idiom legacy's <c>QueryUtils::generateId()</c> (ADOdb
/// <c>GenID</c>) and PatientManagement's advance-directive repository use (LEG-12).
/// </summary>
/// <remarks>
/// Own, short transaction (decision D-CC-2). <c>UPDATE sequences SET id = LAST_INSERT_ID(id + 1)</c> takes a row
/// lock on the single counter row that every legacy writer also needs. Allocating inside the encounter's own
/// transaction would hold that lock until commit and serialize every other writer in the system behind this
/// one's slower multi-table insert. Committing the allocation immediately keeps the lock for microseconds. The
/// cost is that a number allocated and then not used (a failed insert) is skipped, which legacy does too.
/// <para>
/// A transaction is still required: <c>LAST_INSERT_ID()</c> is per connection, so the read must run on the same
/// connection as the update (see <see cref="DatabaseFacadeExtensions.GetLastInsertIdAsync"/>).
/// </para>
/// </remarks>
public sealed class EfEncounterIdentityAllocator(ClinicalCareDbContext db) : IEncounterIdentityAllocator
{
    public async Task<EncounterIdentity> AllocateAsync(CancellationToken ct)
    {
        // The context has EnableRetryOnFailure on, so an explicit transaction must run inside an execution
        // strategy; a retried attempt simply draws a fresh number.
        var strategy = db.Database.CreateExecutionStrategy();
        var number = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sequences SET id = LAST_INSERT_ID(id + 1)", ct);
            var generated = await db.Database.GetLastInsertIdAsync(ct);
            await transaction.CommitAsync(ct);
            return generated;
        });

        // Version 7 is timestamp-first like legacy's COMB uuids (UuidRegistry::createUuid), so rows stay roughly
        // time-ordered in the unique index. Randomness lives here, in the allocator, never in the domain (DOM-8).
        return EncounterIdentity.Create(Guid.CreateVersion7(), number);
    }
}
