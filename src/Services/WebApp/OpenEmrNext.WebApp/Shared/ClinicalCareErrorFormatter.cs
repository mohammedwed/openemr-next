using OpenEmrNext.ClinicalCare.Client;

namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// The ClinicalCare counterpart of <see cref="PatientManagementErrorFormatter"/>: shows the message the API already
/// wrote (its validation errors, or the <c>detail</c> of a 404) instead of a flat "something went wrong". Kept separate
/// because each generated client declares its own <c>ApiException</c> and problem-details types.
/// </summary>
internal static class ClinicalCareErrorFormatter
{
    /// <summary>
    /// Plain text for a failed call. A 503 is what the webapp's circuit breaker returns while the service is known to
    /// be down, so it gets its own wording instead of a generic failure.
    /// </summary>
    public static string Describe(ApiException ex, string fallback)
    {
        if (ex.StatusCode == 503)
        {
            return "The clinical care service is temporarily unavailable. Nothing was saved. Try again in a moment.";
        }

        if (ex is ApiException<HttpValidationProblemDetails> { Result.Errors: { } errors } && errors.Values.SelectMany(v => v).Any())
        {
            return string.Join(" ", errors.Values.SelectMany(v => v));
        }

        return ex is ApiException<ProblemDetails> { Result.Detail: { Length: > 0 } detail } ? detail : fallback;
    }
}
