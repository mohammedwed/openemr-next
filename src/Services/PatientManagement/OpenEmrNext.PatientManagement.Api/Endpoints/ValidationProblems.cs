namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// The one shape every REST endpoint's own validation failure takes -- a <c>ValidationProblem</c> with
/// a single <c>"errors"</c> key -- whether it's one hand-written message (an unparseable wire-format
/// enum value) or the list a Domain factory's own <c>XxxValidationException</c> collected. Previously
/// built inline via <c>Results.ValidationProblem(new Dictionary&lt;string, string[]&gt; { ["errors"] = ... })</c>
/// 8 times across <see cref="PatientEndpoints"/>, <see cref="InsuranceEndpoints"/>, and
/// <see cref="AdvanceDirectiveEndpoints"/>, byte-for-byte identical except for the error text itself.
/// Not used by <see cref="FhirPatientEndpoints"/>, which returns a FHIR <c>OperationOutcome</c> instead
/// -- a genuinely different response shape, not the same duplication.
/// </summary>
internal static class ValidationProblems
{
    public static IResult Single(string error) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [error] });

    public static IResult Many(IReadOnlyList<string> errors) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. errors] });
}
