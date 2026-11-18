using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// The HTTP side of optimistic concurrency for a patient (LEG-10, ADR 0081; the contract Clinical Care uses, ADR 0068): an <c>ETag</c> on a read, <c>If-Match</c> on a write, 428 when it is missing and 412 when the
/// patient has moved on. The version itself, and how a tag is read, is the domain's <see cref="PatientVersion"/>.
/// </summary>
internal static class Preconditions
{
    /// <summary>The patient-version tags in <c>If-Match</c>. <c>false</c> when there is nothing usable to compare against (absent, blank or <c>*</c>, which would switch the protection off): the caller answers <see cref="Required"/>. Tags that are present but unreadable come back as an empty list, which can match nothing: a 412, not a 428.</summary>
    public static bool TryRead(string? header, out IReadOnlyList<string> offered)
    {
        offered = [];
        if (string.IsNullOrWhiteSpace(header) || header.Trim() == "*")
            return false;

        offered = PatientVersion.ParseIfMatch(header);
        return true;
    }

    /// <summary>428: the write must say which version it is based on.</summary>
    public static IResult Required() =>
        Results.Problem(
            title: "A precondition is required.",
            detail: "Send the ETag you read in an If-Match header. An unconditional update (no If-Match, or If-Match: *) is not allowed because other systems edit the same record.",
            statusCode: StatusCodes.Status428PreconditionRequired);

    /// <summary>412: the patient changed after it was read. Carries the current <c>ETag</c> so the caller can reload and apply the change again.</summary>
    public static IResult Failed(string? currentETag)
    {
        var problem = Results.Problem(
            title: "The patient has changed.",
            detail: "Their details were changed after you read them, possibly in the legacy screens. Read them again and apply your change to the current version.",
            statusCode: StatusCodes.Status412PreconditionFailed);
        return currentETag is null ? problem : new ETagResult(problem, currentETag);
    }

    public static IResult Ok<T>(T body, string eTag) => new ETagResult(Results.Ok(body), eTag);

    private sealed class ETagResult(IResult inner, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = etag;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
