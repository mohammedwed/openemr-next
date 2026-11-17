using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Everything a caller supplies to update an encounter: which one, the version the caller read, and the whole editable set. Raw facts:
/// <c>EncounterChange.Create</c> owns the rules.
/// </summary>
/// <param name="Offered">The tags the caller sent in <c>If-Match</c>; the update applies only if one is the current version (LEG-10).</param>
/// <param name="ProviderId">The encounter's provider after the change. Not the person making it (that is the actor).</param>
public sealed record UpdateEncounterCommand(
    Guid EncounterUuid,
    IReadOnlyList<ConcurrencyToken> Offered,
    string? ClassCode,
    int CalendarCategoryId,
    DateTime? Date,
    DateTime? OnsetDate,
    string? Reason,
    int FacilityId,
    int? PosCode,
    long ProviderId,
    RequestOrigin? Origin = null);
