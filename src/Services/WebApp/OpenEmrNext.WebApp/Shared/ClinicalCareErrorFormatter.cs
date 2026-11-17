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
        if (IsStale(ex))
        {
            return "This record was changed by someone else after you opened it, possibly in the legacy screens. Nothing was saved.";
        }

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

    /// <summary>
    /// Whether an update was refused because the record changed after it was read (412). The caller's copy is out of date: it must reload and apply the change again,
    /// not retry as it is (ADR 0068).
    /// </summary>
    public static bool IsStale(ApiException ex) => ex.StatusCode == 412;
}
