namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>A facility an encounter can be held at (legacy <c>facility</c>): its id, name, default place-of-service code and whether legacy marks it a service location.</summary>
public sealed record FacilityLookup(int Id, string Name, int? PosCode, bool IsServiceLocation);

/// <summary>A visit category an encounter can be given (legacy calendar category, <c>pc_catid</c>).</summary>
public sealed record CalendarCategoryLookup(int Id, string Name);
