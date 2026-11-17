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

    private string? _encounterLabel;

    /// <summary>
    /// The label of the encounter on screen ("Encounter 12"), or <c>null</c> on any other page. Set by the encounter
    /// page for the same reason as <see cref="PatientName"/>: the URL only carries the encounter's uuid.
    /// </summary>
    public string? EncounterLabel
    {
        get => _encounterLabel;
        set
        {
            if (_encounterLabel == value)
            {
                return;
            }

            _encounterLabel = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
