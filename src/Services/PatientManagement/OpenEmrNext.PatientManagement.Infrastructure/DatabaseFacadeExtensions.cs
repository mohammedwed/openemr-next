using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// Shared by every repository that writes into a legacy table whose id isn't an EF-owned identity
/// column (<see cref="EfInsurancePolicyRepository.AddAsync"/>,
/// <see cref="EfAdvanceDirectiveRepository.AddDocumentAsync"/>) -- previously duplicated verbatim in
/// both, including the comment explaining why.
/// </summary>
internal static class DatabaseFacadeExtensions
{
    /// <summary>
    /// The id MySQL/MariaDB just generated on the current connection. <c>LAST_INSERT_ID()</c> is
    /// scoped to the connection session, not the statement -- this only returns the right value when
    /// called on the same connection the generating statement itself ran on, which is why every caller
    /// wraps both calls in an explicit transaction first: without one, EF Core is free to run them on
    /// two different pooled connections, reading back someone else's id or none at all (caught the
    /// hard way, against a real database, not by reasoning about it).
    /// </summary>
    public static Task<long> GetLastInsertIdAsync(this DatabaseFacade database, CancellationToken ct) =>
        database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
}
