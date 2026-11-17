using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IIssueDirectory"/>: finds a <c>lists</c> row by uuid, but only one of the two types this service owns (ADR 0070 #2). A medication, a surgery, a
/// health concern or any site-added type is not found, even by its exact uuid. A row with no uuid is not addressable.
/// </summary>
public sealed class EfIssueDirectory(ClinicalCareDbContext db) : IIssueDirectory
{
    public async Task<IssueReference?> FindAsync(Guid issueUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(issueUuid);

        var rows = await db.Database
            .SqlQuery<IssueRow>(
                $"""
                SELECT id AS Id, type AS Type, pid AS Pid, title AS Title
                FROM lists
                WHERE uuid = {uuidBytes} AND type IN ({EfProblemRegistry.ProblemType}, {EfAllergyRegistry.AllergyType})
                """)
            .ToListAsync(ct);

        return rows.Select(r => r is { Pid: > 0 }
                ? new IssueReference(issueUuid, r.Id, r.Type == EfAllergyRegistry.AllergyType ? IssueKind.Allergy : IssueKind.Problem, r.Pid.Value, r.Title)
                : null)
            .FirstOrDefault(r => r is not null);
    }

    private sealed class IssueRow
    {
        public long Id { get; set; }
        public string? Type { get; set; }
        public long? Pid { get; set; }
        public string? Title { get; set; }
    }
}
