using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>Adapter for <see cref="IFacilityDirectory"/>: reads a legacy <c>facility</c> row's name and POS code (LEG-2, design set R10).</summary>
public sealed class EfFacilityDirectory(ClinicalCareDbContext db) : IFacilityDirectory
{
    public async Task<FacilityOption?> FindAsync(int facilityId, CancellationToken ct)
    {
        // facility.name is nullable in legacy; an unnamed facility is stored with an empty name rather than
        // refused, because legacy would copy the NULL and carry on (DOM-9, tolerant read).
        var rows = await db.Database
            .SqlQuery<FacilityRow>(
                $"SELECT id AS Id, name AS Name, CAST(pos_code AS SIGNED) AS PosCode FROM facility WHERE id = {facilityId}")
            .ToListAsync(ct);

        var row = rows.FirstOrDefault();
        return row is null ? null : new FacilityOption(row.Id, row.Name ?? "", row.PosCode is { } pos ? (int)pos : null);
    }

    private sealed class FacilityRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public long? PosCode { get; set; }
    }
}
