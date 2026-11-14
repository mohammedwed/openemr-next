namespace OpenEmrNext.PatientManagement.Domain.Fhir.ValueObjects;

/// <summary>
/// A resolved coded value -- a code from some coding system (SNOMED CT, HL7's NullFlavor, legacy's own
/// race/ethnicity vocabulary, ...), already looked up from <c>list_options</c> and defaulted to legacy's
/// own "Unknown"/data-absent-reason fallback by the Infrastructure adapter that builds a
/// <see cref="PatientFhirView"/>. Used wherever that view would otherwise repeat the same three loose
/// strings for a different extension (race, ethnicity, administrative sex, gender identity, tribal
/// affiliation, interpreter-needed, language) -- six near-identical string triples that are easy to
/// cross-wire by accident when they're just <c>string</c>/<c>string</c>/<c>string?</c>, not a value
/// object.
/// </summary>
/// <remarks>
/// Deliberately named <see cref="CodeSystem"/>/<see cref="CodeValue"/>/<see cref="DisplayText"/>, not
/// FHIR's own <c>Coding.system</c>/<c>.code</c>/<c>.display</c> -- the underlying fact (a code from a
/// named vocabulary, with optional display text) exists independently of FHIR; only
/// <c>FhirPatientMapper.ToCoding</c> (Api layer) translates it into an actual FHIR <c>Coding</c>, which
/// is where that vocabulary belongs.
/// <para/>
/// Validates on construction, same as every other write-side type in this service
/// (<c>Patient.Register</c>, <c>InsurancePolicy.Add</c>, <c>AdvanceDirectiveDocument.Upload</c>):
/// <see cref="Create"/> is the only way to get an instance, so a <see cref="ResolvedCode"/> can never
/// exist with a blank <see cref="CodeSystem"/> or <see cref="CodeValue"/> -- a coded value with either
/// missing is meaningless, not just incomplete. <see cref="DisplayText"/> stays optional: FHIR itself
/// allows a <c>Coding</c> with no display text, and this service never requires one either.
/// </remarks>
public sealed record ResolvedCode
{
    public string CodeSystem { get; }
    public string CodeValue { get; }
    public string? DisplayText { get; }

    private ResolvedCode(string codeSystem, string codeValue, string? displayText)
    {
        CodeSystem = codeSystem;
        CodeValue = codeValue;
        DisplayText = displayText;
    }

    /// <exception cref="ResolvedCodeValidationException">One or more fields fail validation.</exception>
    public static ResolvedCode Create(string codeSystem, string codeValue, string? displayText)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(codeSystem))
            errors.Add("CodeSystem must not be blank.");

        if (string.IsNullOrWhiteSpace(codeValue))
            errors.Add("CodeValue must not be blank.");

        if (errors.Count > 0)
            throw new ResolvedCodeValidationException(errors);

        return new ResolvedCode(codeSystem, codeValue, displayText);
    }
}
