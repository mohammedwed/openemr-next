using OpenEmrNext.PatientManagement.Client;

namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// Surfaces the real validation message(s) PatientManagement.Api already returns (via
/// <c>ValidationProblems</c>/<c>AddValidation()</c>, both wrapped into a typed
/// <c>ApiException&lt;HttpValidationProblemDetails&gt;</c> by the generated client) instead of a flat
/// "something went wrong" -- the Api already did the work of explaining what was wrong; the UI just
/// wasn't showing it.
/// </summary>
internal static class PatientManagementErrorFormatter
{
    /// <summary>
    /// Plain text for a failed call. A 503 is what the webapp's circuit breaker returns while the service is
    /// known to be down, so it gets its own wording instead of a generic failure.
    /// </summary>
    public static string Describe(ApiException ex, string fallback)
    {
        if (ex.StatusCode == 503)
        {
            return "The patient service is temporarily unavailable. Nothing was saved. Try again in a moment.";
        }

        return ex is ApiException<HttpValidationProblemDetails> { Result.Errors: { } errors } && errors.Values.SelectMany(v => v).Any()
            ? string.Join(" ", errors.Values.SelectMany(v => v))
            : fallback;
    }
}
