using System.Net;
using System.Text.Json;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Infrastructure;

namespace OpenEmrNext.PatientManagement.UnitTests.Relay;

/// <summary>The HTTP adapter's wire contract and its decision about which failures are worth retrying (EVT-7).</summary>
public class HttpEventPublisherTests
{
    private static readonly OutboundEvent Event = new(
        Guid.Parse("a1a1a1a1-0000-5000-8000-000000000001"), "PatientRegistered", 1,
        new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc), "b1a9c5f0-0000-4000-8000-0000000000aa", null, "corr",
        """{"uuid":"b1a9c5f0-0000-4000-8000-0000000000aa","pid":42,"firstName":"A","lastName":"B","registeredAtUtc":"2026-11-16T09:00:00Z"}""");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpEventPublisher Publisher(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://clinical-care:8080") });

    [Fact]
    public async Task The_event_is_posted_to_the_internal_events_path_with_the_pinned_envelope_names()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await Publisher(handler).PublishAsync(Event, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://clinical-care:8080/internal/events", handler.Request.RequestUri!.ToString());
        var root = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal(
            ["actor", "causationId", "correlationId", "eventId", "eventType", "occurredAt", "payload", "schemaVersion", "subject"],
            root.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(Event.EventId, root.GetProperty("eventId").GetGuid());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(42, root.GetProperty("payload").GetProperty("pid").GetInt64());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task Any_success_status_means_delivered(HttpStatusCode status)
    {
        await Publisher(new StubHandler(_ => new HttpResponseMessage(status))).PublishAsync(Event, CancellationToken.None);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task A_rejection_of_the_event_itself_is_permanent(HttpStatusCode status)
    {
        var ex = await Assert.ThrowsAsync<EventPublishException>(() =>
            Publisher(new StubHandler(_ => new HttpResponseMessage(status))).PublishAsync(Event, CancellationToken.None));

        Assert.True(ex.IsPermanent);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_wrong_secret_a_missing_route_or_an_unhealthy_consumer_is_transient_never_a_dead_letter(HttpStatusCode status)
    {
        // An operator fixes the secret or finishes the deploy while the backlog waits; dead-lettering would throw good events away.
        var ex = await Assert.ThrowsAsync<EventPublishException>(() =>
            Publisher(new StubHandler(_ => new HttpResponseMessage(status))).PublishAsync(Event, CancellationToken.None));

        Assert.False(ex.IsPermanent);
    }

    [Fact]
    public async Task A_network_failure_is_transient()
    {
        var ex = await Assert.ThrowsAsync<EventPublishException>(() =>
            Publisher(new StubHandler(_ => throw new HttpRequestException("connection refused"))).PublishAsync(Event, CancellationToken.None));

        Assert.False(ex.IsPermanent);
        Assert.Contains("connection refused", ex.Message);
    }
}
