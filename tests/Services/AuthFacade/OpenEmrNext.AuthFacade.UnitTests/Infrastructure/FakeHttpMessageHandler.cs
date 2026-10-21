namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>. It records the last request and its body,
/// then throws <see cref="Exception"/> if one is set, or returns <see cref="Response"/>.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>The response to return. Must be set when no exception is.</summary>
    public HttpResponseMessage? Response { get; set; }

    /// <summary>When set, thrown from the send instead of returning <see cref="Response"/>.</summary>
    public Exception? Exception { get; set; }

    /// <summary>The last request sent through the handler.</summary>
    public HttpRequestMessage? Request { get; private set; }

    /// <summary>The last request's body as a string, or null when the request had no body.</summary>
    public string? RequestBody { get; private set; }

    /// <summary>Records the request and its body, then throws or returns the configured response.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        if (Exception is not null)
        {
            throw Exception;
        }

        return Response!;
    }
}
