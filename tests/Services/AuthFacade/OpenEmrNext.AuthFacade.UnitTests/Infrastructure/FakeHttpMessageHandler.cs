namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage? Response { get; set; }
    public Exception? Exception { get; set; }

    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }

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
