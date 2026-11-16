using System.Text.Json;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The envelope every event arrives in (EVT-2, playbook 13 section 12). Only what this consumer acts on is declared;
/// unknown fields are ignored, so a producer may add envelope fields without breaking us (a tolerant reader).
/// </summary>
/// <param name="EventId">A stable id for this event, the same on every redelivery. The idempotency key (EVT-3).</param>
/// <param name="EventType">For example <c>PatientRegistered</c>.</param>
/// <param name="SchemaVersion">The payload's schema version. This consumer understands version 1.</param>
/// <param name="Payload">The event body, interpreted according to <paramref name="EventType"/>.</param>
public sealed record IncomingEventRequest(Guid EventId, string? EventType, int SchemaVersion, JsonElement Payload);

/// <summary>The <c>PatientRegistered</c> payload as Clinical Care reads it: identity only. The producer also sends names; they are ignored (EVT-5, design set D4).</summary>
public sealed record PatientRegisteredPayload(Guid Uuid, long Pid, DateTime? RegisteredAtUtc);
