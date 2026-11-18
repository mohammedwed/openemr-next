using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IEncounterLookups"/> (LEG-2): three parameterless, bounded reads of legacy configuration tables (facilities, visit categories and providers). Both lists are small configuration, capped at <see cref="MaxRows"/> so a
/// misconfigured install cannot make a lookup unbounded (API-6).
/// </summary>
/// <remarks>
/// Categories are the ones legacy's visit form offers: active, <c>pc_cattype = 0</c> (patient visits; type 3 is therapy groups, a feature this service does not offer; types 1 and 2 are provider and clinic events),
/// not <c>no_show</c>, in <c>pc_seq</c> order. Facilities are the non-inactive ones. A facility with no name reads as an empty name (DOM-9).
/// </remarks>
public sealed class EfEncounterLookups(ClinicalCareDbContext db) : IEncounterLookups
{
    public const int MaxRows = 500;

    public async Task<IReadOnlyList<FacilityLookup>> GetFacilitiesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<FacilityRow>(
                $"""
                SELECT id AS Id, name AS Name, CAST(pos_code AS SIGNED) AS PosCode, CAST(service_location AS SIGNED) AS ServiceLocation
                FROM facility WHERE inactive = 0 ORDER BY name, id LIMIT {MaxRows}
                """)
            .ToListAsync(ct);

        return rows.Select(r => new FacilityLookup(r.Id, r.Name ?? "", r.PosCode is { } pos ? (int)pos : null, r.ServiceLocation != 0)).ToList();
    }

    public async Task<IReadOnlyList<CalendarCategoryLookup>> GetCalendarCategoriesAsync(CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQuery<CategoryRow>(
                $"""
                SELECT pc_catid AS Id, pc_catname AS Name
                FROM openemr_postcalendar_categories
                WHERE pc_active = 1 AND pc_cattype = 0 AND (pc_constant_id IS NULL OR pc_constant_id <> 'no_show')
                ORDER BY pc_seq, pc_catid LIMIT {MaxRows}
                """)
            .ToListAsync(ct);

        return rows.Select(r => new CalendarCategoryLookup(r.Id, r.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ProviderLookup>> GetProvidersAsync(CancellationToken ct)
    {
        // Legacy's own provider lists filter the same way: an active, authorized user with a username. The grant is on these columns only; nothing about the account beyond the name is read.
        var rows = await db.Database
            .SqlQuery<ProviderRow>(
                $"""
                SELECT id AS Id, fname AS FirstName, lname AS LastName, username AS Username
                FROM users WHERE active = 1 AND authorized = 1 AND username IS NOT NULL AND username <> ''
                ORDER BY lname, fname, id LIMIT {MaxRows}
                """)
            .ToListAsync(ct);

        return rows.Select(r => new ProviderLookup(r.Id, r.FirstName?.Trim() ?? "", r.LastName?.Trim() ?? "", r.Username ?? "")).ToList();
    }

    private sealed class ProviderRow
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
    }

    private sealed class FacilityRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public long? PosCode { get; set; }
        public long ServiceLocation { get; set; }
    }

    private sealed class CategoryRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
