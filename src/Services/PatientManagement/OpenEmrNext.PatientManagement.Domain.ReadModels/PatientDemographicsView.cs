using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.ReadModels;

/// <summary>
/// A patient's editable demographics as they are stored, with the version they are at (ADR 0081). <paramref name="PublicPatientId"/> is shown with them but is not editable here and is not part of the version. <paramref name="DateOfBirth"/> is <c>null</c> when legacy holds none (or a date that is not one, such as
/// <c>0000-00-00</c>); a legacy row is never refused for what it holds, only read as it is.
/// </summary>
public sealed record PatientDemographicsView(
    Guid Uuid, long Pid, string FirstName, string LastName, string Sex, DateOnly? DateOfBirth, string? Email, string? TrustedEmail, string? PublicPatientId = null)
{
    /// <summary>
    /// The names, in wire form, of the fields <paramref name="change"/> would change: what an audit row lists, never the values. Empty when nothing would change. Compared against what is stored as it is, so a legacy
    /// row that registration's rules would not have admitted can still be corrected.
    /// </summary>
    public IReadOnlyList<string> ChangedBy(PatientDemographics change)
    {
        var changed = new List<string>();
        if (!string.Equals(FirstName, change.FirstName, StringComparison.Ordinal)) changed.Add("firstName");
        if (!string.Equals(LastName, change.LastName, StringComparison.Ordinal)) changed.Add("lastName");
        if (!string.Equals(Sex, change.Sex, StringComparison.Ordinal)) changed.Add("sex");
        if (DateOfBirth != change.DateOfBirth) changed.Add("dateOfBirth");
        if (!string.Equals(Email ?? "", change.Email ?? "", StringComparison.Ordinal)) changed.Add("email");
        if (!string.Equals(TrustedEmail ?? "", change.TrustedEmail ?? "", StringComparison.Ordinal)) changed.Add("trustedEmail");
        return changed;
    }

    public PatientVersion Version() => PatientVersion.Of(
        [FirstName, LastName, Sex, DateOfBirth?.ToString("yyyy-MM-dd"), Email ?? "", TrustedEmail ?? ""]);
}
