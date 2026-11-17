using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.IssueLinks;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterIssueReader"/>: the problems and allergies linked to one encounter, read tolerantly (DOM-9, LEG-4). Only the two owned issue types appear
/// (ADR 0070 #1), and an issue with no uuid is not addressable. The join is on the issue's row id <b>and</b> its patient, so a link between two different patients' records, which
/// legacy allows and this service forbids, is never shown, and a link whose issue legacy's REST delete removed (it leaves the link behind) is left out.
/// </summary>
public sealed class EfEncounterIssueReader(ClinicalCareDbContext db) : IEncounterIssueReader
{
    public async Task<EncounterIssuePage> ListForEncounterAsync(long encounterNumber, long patientPid, PageRequest page, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<Row>(
                $"""
                SELECT ie.uuid AS LinkUuid, l.uuid AS IssueUuid, l.type AS Type, l.title AS Title, ie.resolved AS Resolved, ie.created_at AS LinkedAt
                FROM issue_encounter ie
                JOIN lists l ON l.id = ie.list_id AND l.pid = ie.pid
                WHERE ie.pid = {patientPid} AND ie.encounter = {encounterNumber}
                  AND l.type IN ({EfProblemRegistry.ProblemType}, {EfAllergyRegistry.AllergyType}) AND l.uuid IS NOT NULL
                ORDER BY ie.id
                LIMIT {page.Limit} OFFSET {page.Offset}
                """)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>(
                $"""
                SELECT COUNT(*) AS Value
                FROM issue_encounter ie
                JOIN lists l ON l.id = ie.list_id AND l.pid = ie.pid
                WHERE ie.pid = {patientPid} AND ie.encounter = {encounterNumber}
                  AND l.type IN ({EfProblemRegistry.ProblemType}, {EfAllergyRegistry.AllergyType}) AND l.uuid IS NOT NULL
                """)
            .SingleAsync(ct);

        return new EncounterIssuePage(rows.Select(ToView).ToList(), (int)total);
    }

    private static EncounterIssueView ToView(Row row) => new(
        row.LinkUuid is { Length: 16 } link ? Rfc4122GuidConverter.FromRfc4122Bytes(link) : null,
        Rfc4122GuidConverter.FromRfc4122Bytes(row.IssueUuid!),
        row.Type == EfAllergyRegistry.AllergyType ? IssueKind.Allergy : IssueKind.Problem,
        row.Title,
        row.Resolved != 0,
        row.LinkedAt);

    private sealed class Row
    {
        public byte[]? LinkUuid { get; set; }
        public byte[]? IssueUuid { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int Resolved { get; set; }
        public DateTime? LinkedAt { get; set; }
    }
}
