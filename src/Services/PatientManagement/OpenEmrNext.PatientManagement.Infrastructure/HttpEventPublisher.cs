using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The HTTP adapter for <see cref="IEventPublisher"/>: POSTs the event, as the consumer's contract defines it, to
/// <c>/internal/events</c> (ADR 0066). The base address, timeout and secret header are set on the <see cref="HttpClient"/>
/// at the composition root, so this class holds no configuration. Wire names are pinned here, in one place (ARC-7).
/// </summary>
public sealed class HttpEventPublisher(HttpClient http) : IEventPublisher
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(OutboundEvent @event, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(@event.PayloadJson);
        var body = new
        {
            eventId = @event.EventId,
            eventType = @event.EventType,
            schemaVersion = @event.SchemaVersion,
            occurredAt = @event.OccurredAtUtc,
            subject = @event.Subject,
            actor = @event.Actor,
            correlationId = @event.CorrelationId,
            causationId = (string?)null,
            payload = payload.RootElement
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/internal/events", body, Wire, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            // The consumer is down, slow or unreachable: the path is at fault, not the event.
            throw new EventPublishException($"The event consumer could not be reached: {ex.Message}", isPermanent: false, ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return;

            throw new EventPublishException(
                $"The event consumer answered {(int)response.StatusCode} {response.ReasonPhrase}.", IsPermanent(response.StatusCode));
        }
    }

    /// <summary>
    /// Only a rejection of **the event itself** is permanent: 400 (malformed or unsupported) and 422. Everything else,
    /// including 401, 403 and 404, is the receiver's deployment or configuration, which an operator fixes while the backlog
    /// waits; dead-lettering on a wrong secret would throw away events that were perfectly good.
    /// </summary>
    private static bool IsPermanent(HttpStatusCode status) => status is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;
}
