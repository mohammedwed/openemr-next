namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>One unpublished row of this service's outbox, as the relay claims it.</summary>
/// <param name="Id">The row id: strictly increasing, which is the delivery order.</param>
/// <param name="EventType">For example <c>PatientRegistered</c>.</param>
/// <param name="Payload">The JSON the writer stored. Older rows use PascalCase names and have no envelope; see <c>OutboundEventFactory</c>.</param>
/// <param name="OccurredAtUtc">When the event happened (UTC).</param>
/// <param name="Attempts">How many times delivery has already been tried.</param>
public sealed record OutboxEntry(long Id, string EventType, string Payload, DateTime OccurredAtUtc, int Attempts);

/// <summary>
/// An event as it travels (EVT-2, playbook 13 section 12): a stable id the consumer dedupes on (EVT-3), a type and
/// <see cref="SchemaVersion"/> to branch on, and the curated <see cref="PayloadJson"/>. The wire names are pinned by
/// the publisher adapter, not by this type.
/// </summary>
public sealed record OutboundEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTime OccurredAtUtc,
    string Subject,
    string? Actor,
    string CorrelationId,
    string PayloadJson);
