using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.ReadModels;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientDemographicsStore"/> and the anti-corruption layer for correcting a patient (LEG-2, ADR 0081): one transaction that locks the patient's row, checks it is still the version the
/// caller read, writes the six columns this service owns, and appends the audit row.
/// </summary>
/// <remarks>
/// <para><c>patient_data</c> has about 130 columns and many writers, so the statement names only <c>fname</c>, <c>lname</c>, <c>sex</c>, <c>DOB</c>, <c>email</c>, <c>email_direct</c> and <c>date</c>, the table's own
/// last-updated column, which legacy's writers set to the moment of the change. Everything else about the patient is left as the other writers have it.</para>
/// <para>The version is a hash of the six owned values, read under <c>FOR UPDATE</c> in the same transaction (<see cref="Domain.ValueObjects.PatientVersion"/>), so a legacy edit of one of them between the caller's read
/// and this write is a conflict, and an edit of anything else is not. Two simultaneous corrections cannot both succeed. A date of birth legacy holds as <c>0000-00-00</c> or any other non-date is read as none.</para>
/// </remarks>
public sealed class EfPatientDemographicsStore(PatientManagementDbContext db, IAuditLog auditLog) : IPatientDemographicsStore
{
    private const string Columns =
        "pid AS Pid, uuid AS Uuid, fname AS FirstName, lname AS LastName, sex AS Sex, CAST(DOB AS CHAR(10)) AS DateOfBirth, email AS Email, email_direct AS TrustedEmail, pubpid AS PublicPatientId";

    public async Task<PatientDemographicsView?> FindAsync(Guid uuid, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQueryRaw<Row>("SELECT " + Columns + " FROM patient_data WHERE uuid = {0}", Rfc4122GuidConverter.ToRfc4122Bytes(uuid))
            .ToListAsync(ct);

        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<DemographicsUpdateOutcome> UpdateAsync(DemographicsUpdateRequest request, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(request.Uuid);
        var c = request.Change;

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var rows = await db.Database
                .SqlQueryRaw<Row>("SELECT " + Columns + " FROM patient_data WHERE uuid = {0} FOR UPDATE", uuidBytes)
                .ToListAsync(ct);
            if (rows.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return new DemographicsUpdateOutcome(DemographicsUpdateStatus.NotFound, null);
            }

            var current = ToView(rows[0]);
            var currentVersion = current.Version().ToETag();
            if (!request.OfferedVersions.Contains(currentVersion, StringComparer.Ordinal))
            {
                await transaction.RollbackAsync(ct);
                return new DemographicsUpdateOutcome(DemographicsUpdateStatus.Conflict, currentVersion);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE patient_data
                SET fname = {c.FirstName}, lname = {c.LastName}, sex = {c.Sex}, DOB = {c.DateOfBirth.ToString("yyyy-MM-dd")},
                    email = {c.Email ?? ""}, email_direct = {c.TrustedEmail ?? ""}, `date` = {request.NowUtc}
                WHERE pid = {current.Pid} AND uuid = {uuidBytes}
                """, ct);

            // Last statement, same transaction: the correction cannot commit without its audit row (SEC-9, ADR 0078).
            await auditLog.AppendAsync(request.Audit, ct);

            await transaction.CommitAsync(ct);
            return new DemographicsUpdateOutcome(DemographicsUpdateStatus.Updated, null);
        });
    }

    private static PatientDemographicsView ToView(Row r) =>
        new(Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid!), r.Pid, r.FirstName ?? "", r.LastName ?? "", r.Sex ?? "",
            DateOnly.TryParseExact(r.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob) ? dob : null,
            string.IsNullOrEmpty(r.Email) ? null : r.Email, string.IsNullOrEmpty(r.TrustedEmail) ? null : r.TrustedEmail,
            string.IsNullOrEmpty(r.PublicPatientId) ? null : r.PublicPatientId);

    private sealed class Row
    {
        public long Pid { get; set; }
        public byte[]? Uuid { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Sex { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Email { get; set; }
        public string? TrustedEmail { get; set; }
        public string? PublicPatientId { get; set; }
    }
}
