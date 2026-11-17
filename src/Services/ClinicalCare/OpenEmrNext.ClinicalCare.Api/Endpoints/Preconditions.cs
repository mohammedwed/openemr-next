using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// The HTTP side of optimistic concurrency (LEG-10, ADR 0068): <c>ETag</c> on a single-record read, <c>If-Match</c> on a write, 428 when it is
/// missing and 412 when the record has moved on. The token itself, and how it is parsed, is the domain's <see cref="ConcurrencyToken"/>; this
/// class only moves it to and from headers and shapes the two problem responses.
/// </summary>
internal static class Preconditions
{
    /// <summary>A 200 carrying <paramref name="body"/> and, when the row has a stamp, its <c>ETag</c>.</summary>
    public static IResult Ok<T>(T body, DateTime? stamp) => WithETag(Results.Ok(body), stamp);

    /// <summary>
    /// Reads the tags a write offered in <c>If-Match</c>. Returns <c>false</c> when there is nothing usable to compare against: the header is
    /// absent or blank, or it is <c>*</c>, which would switch the protection off ("any current version"). An unconditional update is never allowed
    /// (ADR 0068 #3), so the caller answers <see cref="Required"/>. Tags that are present but unreadable come back as an empty list: they cannot
    /// match anything, which is a 412 (the caller's version is not current), not a 428.
    /// </summary>
    public static bool TryRead(HttpRequest request, out IReadOnlyList<ConcurrencyToken> offered)
    {
        offered = [];
        var header = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(header) || header.Trim() == "*")
            return false;

        offered = ConcurrencyToken.ParseIfMatch(header);
        return true;
    }

    /// <summary>428: the write must say which version it is based on.</summary>
    public static IResult Required() =>
        Results.Problem(
            title: "A precondition is required.",
            detail: "Send the ETag you read in an If-Match header. An unconditional update (no If-Match, or If-Match: *) is not allowed because " +
                    "other systems edit the same record.",
            statusCode: StatusCodes.Status428PreconditionRequired);

    /// <summary>412: the record changed after it was read. Carries its current <c>ETag</c> so the caller can reload and apply the change again.</summary>
    public static IResult Failed(DateTime? currentStamp) =>
        WithETag(
            Results.Problem(
                title: "The record has changed.",
                detail: "It was changed after you read it, possibly in the legacy screens or by a billing action. Read it again and apply your change to the current version.",
                statusCode: StatusCodes.Status412PreconditionFailed),
            currentStamp);

    private static IResult WithETag(IResult inner, DateTime? stamp) =>
        stamp is null ? inner : new ETagResult(inner, ConcurrencyToken.From(stamp.Value).ToETag());

    private sealed class ETagResult(IResult inner, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = etag;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
