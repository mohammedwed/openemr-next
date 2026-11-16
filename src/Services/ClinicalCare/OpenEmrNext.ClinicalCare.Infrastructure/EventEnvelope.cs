using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The wrapper every outbox payload travels in (EVT-2, playbook 13 §12), so a consumer can dedupe on
/// <see cref="EventId"/> (EVT-3), branch on <see cref="SchemaVersion"/>, and trace across services.
/// Built where the outbox row is written, so the event type itself (<c>EncounterCreatedEvent</c>) stays a pure,
/// curated payload.
/// </summary>
internal sealed record EventEnvelope(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("occurredAt")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("actor")] string? Actor,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("causationId")] string? CausationId,
    [property: JsonPropertyName("payload")] JsonElement Payload)
{
    public const int CurrentSchemaVersion = 1;

    // camelCase, pinned explicitly: an event is a public contract (ARC-7, EVT-5), so its field names must not
    // depend on a serializer default that a future change could alter.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<TPayload>(
        string eventType, DateTime occurredAtUtc, string subject, string? actor, TPayload payload)
    {
        var envelope = new EventEnvelope(
            Guid.NewGuid(), eventType, CurrentSchemaVersion, occurredAtUtc, subject, actor,
            // The caller's trace id when there is one, so a request can be followed across services; a fresh id otherwise.
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            CausationId: null,
            JsonSerializer.SerializeToElement(payload, Options));

        return JsonSerializer.Serialize(envelope, Options);
    }
}
