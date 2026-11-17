namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// The one shape every endpoint's own validation failure takes: a <c>ValidationProblem</c> with a single
/// <c>"errors"</c> key holding every message a domain factory's <c>ClinicalCareValidationException</c> collected
/// (DOM-3, API-4). Same shape as PatientManagement's, so clients handle both services' 400s alike.
/// </summary>
internal static class ValidationProblems
{
    public static IResult Single(string error) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [error] });

    public static IResult Many(IReadOnlyList<string> errors) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. errors] });
}
