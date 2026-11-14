namespace OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

/// <summary>
/// A resolved coded value -- a FHIR <c>Coding</c>'s three parts, already looked up from
/// <c>list_options</c> and defaulted to legacy's own "Unknown"/data-absent-reason fallback by the
/// Infrastructure adapter that builds a <see cref="PatientFhirView"/>. Used wherever that view would
/// otherwise repeat the same three loose strings for a different extension (race, ethnicity's
/// ombCategory, administrative sex, gender identity, tribal affiliation, interpreter-needed,
/// language) -- six near-identical string triples that are easy to cross-wire by accident when they're
/// just <c>string</c>/<c>string</c>/<c>string?</c>, not a value object.
/// </summary>
/// <remarks>
/// Validates on construction, same as every other write-side type in this service
/// (<c>Patient.Register</c>, <c>InsurancePolicy.Add</c>, <c>AdvanceDirectiveDocument.Upload</c>):
/// <see cref="Create"/> is the only way to get an instance, so a <see cref="ResolvedCode"/> can never
/// exist with a blank <see cref="System"/> or <see cref="Code"/> -- a FHIR <c>Coding</c> with either
/// missing is meaningless, not just incomplete. <see cref="Display"/> stays optional: FHIR itself
/// allows a <c>Coding</c> with no display text.
/// </remarks>
public sealed record ResolvedCode
{
    public string System { get; }
    public string Code { get; }
    public string? Display { get; }

    private ResolvedCode(string system, string code, string? display)
    {
        System = system;
        Code = code;
        Display = display;
    }

    /// <exception cref="ResolvedCodeValidationException">One or more fields fail validation.</exception>
    public static ResolvedCode Create(string system, string code, string? display)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(system))
            errors.Add("System must not be blank.");

        if (string.IsNullOrWhiteSpace(code))
            errors.Add("Code must not be blank.");

        if (errors.Count > 0)
            throw new ResolvedCodeValidationException(errors);

        return new ResolvedCode(system, code, display);
    }
}
