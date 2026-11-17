namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Everything a caller supplies to create an encounter. Raw facts: <c>EncounterCreation.Create</c> owns the rules.</summary>
public sealed record CreateEncounterCommand(
    Guid PatientUuid,
    string? ClassCode,
    int CalendarCategoryId,
    DateTime? Date,
    string? Reason,
    int FacilityId,
    int? PosCode,
    long ProviderId,
    DateTime? OnsetDate,
    string? Sensitivity,
    RequestOrigin? Origin = null);
