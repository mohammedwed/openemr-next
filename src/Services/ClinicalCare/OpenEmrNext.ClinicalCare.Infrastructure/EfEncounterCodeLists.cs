using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Adapter for <see cref="IEncounterCodeLists"/>: reads legacy's <c>_ActEncounterCode</c> list (LEG-2, design set R10).</summary>
public sealed class EfEncounterCodeLists(ClinicalCareDbContext db) : IEncounterCodeLists
{
    public async Task<IReadOnlyList<EncounterClassOption>> GetEncounterClassesAsync(CancellationToken ct)
    {
        // No `activity` filter, matching legacy (ListService::getOptionsByListName, BaseValidator::validateCode).
        var rows = await db.Database
            .SqlQuery<ClassRow>(
                $"""
                SELECT option_id AS Code, title AS Title, CAST(is_default AS SIGNED) AS IsDefault
                FROM list_options WHERE list_id = '_ActEncounterCode' ORDER BY seq
                """)
            .ToListAsync(ct);

        return rows.Select(r => new EncounterClassOption(r.Code, r.Title, r.IsDefault != 0)).ToList();
    }

    private sealed class ClassRow
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public long IsDefault { get; set; }
    }
}
