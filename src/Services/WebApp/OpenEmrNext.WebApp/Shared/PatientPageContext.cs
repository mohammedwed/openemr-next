namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// Lets the patient page tell the shared layout who it is showing, so the breadcrumb can name the patient.
/// The layout builds its breadcrumb from the URL alone, which only has the patient's ID.
/// </summary>
public sealed class PatientPageContext
{
    private string? _patientName;

    /// <summary>The name of the patient on screen, or <c>null</c> while loading or on another page.</summary>
    public string? PatientName
    {
        get => _patientName;
        set
        {
            if (_patientName == value)
            {
                return;
            }

            _patientName = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
