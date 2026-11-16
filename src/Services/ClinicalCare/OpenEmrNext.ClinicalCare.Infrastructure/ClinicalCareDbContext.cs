using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The connection to OpenEMR's shared <c>openemr</c> database, through the least-privilege user the
/// <c>seed-clinical-care-db</c> job provisions (ADR 0064 #1).
/// </summary>
/// <remarks>
/// Deliberately has no mapped entities. Every legacy table here (<c>form_encounter</c>, <c>forms</c>,
/// <c>uuid_registry</c>, <c>sequences</c>, ...) is owned by OpenEMR and written through explicit, parameterized
/// SQL (LEG-5) so the exact statements, including legacy's two-table write, stay visible and reviewable in one
/// adapter (LEG-2). Nothing here is ever migrated (LEG-1): there is no model to migrate. The context supplies
/// the connection, retry strategy and transaction handling. Entities for Clinical Care's own tables (the
/// outbox relay, the patient reference) are added when slice 3 needs to query them.
/// </remarks>
public sealed class ClinicalCareDbContext(DbContextOptions<ClinicalCareDbContext> options) : DbContext(options);
