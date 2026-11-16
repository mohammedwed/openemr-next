using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>The outbox row cannot ever be delivered as it stands (for example, a payload that is not the event it claims to be). Retrying cannot help.</summary>
public sealed class UnpublishableEventException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Turns an outbox row into the event that travels (EVT-2). A pure function (DOM-12) so it is tested without a database.
/// </summary>
/// <remarks>
/// <b>Why this exists at all:</b> the outbox was written before any consumer existed (design set D2). Its payload has
/// PascalCase names and no envelope, and the rows already in it must still be deliverable. So the envelope is built
/// **here, at publish time**, from the row, and the payload is read tolerantly and re-written with the pinned camelCase
/// names. New and old rows therefore leave identically.
/// </remarks>
public static class OutboundEventFactory
{
    /// <summary>The schema version of every event this service emits today (EVT-2).</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static OutboundEvent From(OutboxEntry entry)
    {
        var (subject, payloadJson) = entry.EventType switch
        {
            PatientRegisteredEvent.EventType => NormalizePatientRegistered(entry),
            // An event type this version does not know is forwarded as stored: the consumer decides whether it cares.
            _ => (entry.Id.ToString(), entry.Payload)
        };

        var eventId = EventIdFor(entry.Id);
        return new OutboundEvent(
            eventId, entry.EventType, SchemaVersion, DateTime.SpecifyKind(entry.OccurredAtUtc, DateTimeKind.Utc),
            subject, Actor: null, CorrelationId: eventId.ToString("N"), payloadJson);
    }

    /// <summary>
    /// A stable id for an outbox row: the same row always yields the same event id, on every retry and from every relay
    /// instance, which is what lets a consumer dedupe a redelivery (EVT-3). Derived by hashing the row id under a fixed
    /// namespace, formatted as a name-based (version 5) uuid. Assumes the outbox is append-only and its ids are never
    /// reused (finding F-PM-4).
    /// </summary>
    public static Guid EventIdFor(long outboxId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"openemr-next/patient-management/outbox/{outboxId}"));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        // Guid's byte order is mixed-endian; build it from the RFC order the bytes were laid out in.
        return new Guid(
            (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3],
            (short)((bytes[4] << 8) | bytes[5]), (short)((bytes[6] << 8) | bytes[7]),
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
    }

    private static (string Subject, string PayloadJson) NormalizePatientRegistered(OutboxEntry entry)
    {
        PatientRegisteredEvent? parsed;
        try
        {
            // Web defaults read names case-insensitively, so rows written with PascalCase and with camelCase both parse.
            parsed = JsonSerializer.Deserialize<PatientRegisteredEvent>(entry.Payload, Wire);
        }
        catch (JsonException ex)
        {
            throw new UnpublishableEventException($"Outbox row {entry.Id} is not a valid {entry.EventType} payload.", ex);
        }

        if (parsed is null || parsed.Uuid == Guid.Empty || parsed.Pid <= 0)
            throw new UnpublishableEventException($"Outbox row {entry.Id} names no usable patient.");

        return (parsed.Uuid.ToString(), JsonSerializer.Serialize(parsed, Wire));
    }
}
