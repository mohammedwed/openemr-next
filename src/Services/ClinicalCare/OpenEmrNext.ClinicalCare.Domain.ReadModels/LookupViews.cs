namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>A facility an encounter can be held at (legacy <c>facility</c>): its id, name, default place-of-service code and whether legacy marks it a service location.</summary>
public sealed record FacilityLookup(int Id, string Name, int? PosCode, bool IsServiceLocation);

/// <summary>A visit category an encounter can be given (legacy calendar category, <c>pc_catid</c>).</summary>
public sealed record CalendarCategoryLookup(int Id, string Name);

/// <summary>
/// A user an encounter can name as its provider (legacy <c>users</c> with <c>authorized = 1</c> and <c>active = 1</c>). The display name is "First Last"; a user legacy has recorded with only one of the two shows
/// that one, and one with neither shows the username, so a caller always has something to show.
/// </summary>
public sealed record ProviderLookup(long Id, string FirstName, string LastName, string Username)
{
    public string DisplayName => string.Join(' ', new[] { FirstName, LastName }.Where(n => !string.IsNullOrWhiteSpace(n))) is { Length: > 0 } name ? name : Username;
}
