using System.Text;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.SoapNotes;

/// <summary>The four sections a caller supplies, raw and unchecked: <see cref="SoapSections.Create"/> owns the rules.</summary>
public sealed record SoapInput(string? Subjective = null, string? Objective = null, string? Assessment = null, string? Plan = null);

/// <summary>
/// The validated sections of a SOAP note, in the form they are stored. <see cref="Create"/> is the only way to get one, so recording and correcting a note cannot disagree about what is valid (one owner per
/// rule, DOM-2), and every violation is collected before failing (DOM-3). Rules SN-1 and SN-2, docs/clinical-care/17-soap-notes-slice.md.
/// </summary>
public sealed record SoapSections
{
    /// <summary>Legacy's lower bound for a section that is given (its REST validator: 2 to 65,535).</summary>
    public const int MinLength = 2;

    /// <summary>The columns are <c>text</c>: 65,535 <b>bytes</b>, not characters, so the limit is the UTF-8 size (review findings R-1 to R-7).</summary>
    public const int MaxBytes = 65_535;

    public string? Subjective { get; }
    public string? Objective { get; }
    public string? Assessment { get; }
    public string? Plan { get; }

    private SoapSections(string? subjective, string? objective, string? assessment, string? plan)
    {
        Subjective = subjective;
        Objective = objective;
        Assessment = assessment;
        Plan = plan;
    }

    /// <exception cref="ClinicalCareValidationException">No section has text, or one is too short or too long.</exception>
    public static SoapSections Create(SoapInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<string>();
        var subjective = Section(input.Subjective, "Subjective", errors);
        var objective = Section(input.Objective, "Objective", errors);
        var assessment = Section(input.Assessment, "Assessment", errors);
        var plan = Section(input.Plan, "Plan", errors);

        // SN-1: an entirely empty note is meaningless. A note whose only section was refused already has a message.
        if (errors.Count == 0 && subjective is null && objective is null && assessment is null && plan is null)
            errors.Add("At least one section of the note is required.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new SoapSections(subjective, objective, assessment, plan);
    }

    /// <summary>Blank means not written. Text is kept exactly as given (clinicians format notes), but must have two characters of substance.</summary>
    private static string? Section(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.Trim().Length < MinLength)
        {
            errors.Add($"{label} must be at least {MinLength} characters.");
            return null;
        }

        if (Encoding.UTF8.GetByteCount(value) > MaxBytes)
        {
            errors.Add($"{label} must be at most {MaxBytes} bytes.");
            return null;
        }

        return value;
    }
}
