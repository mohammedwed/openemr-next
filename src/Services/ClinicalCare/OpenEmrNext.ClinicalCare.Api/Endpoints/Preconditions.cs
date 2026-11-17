using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// The HTTP side of optimistic concurrency (LEG-10, ADR 0068). For now: the <c>ETag</c> on a single-record read. The token itself, and how
/// it is parsed, is the domain's <see cref="ConcurrencyToken"/>; this class only moves it to and from headers. <c>If-Match</c>, the 428 and
/// the 412 arrive with the first write endpoint, where they can be pinned through real HTTP.
/// </summary>
internal static class Preconditions
{
    /// <summary>A 200 carrying <paramref name="body"/> and, when the row has a stamp, its <c>ETag</c>.</summary>
    public static IResult Ok<T>(T body, DateTime? stamp) =>
        stamp is null ? Results.Ok(body) : new ETagResult(Results.Ok(body), ConcurrencyToken.From(stamp.Value).ToETag());

    private sealed class ETagResult(IResult inner, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.ETag = etag;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
