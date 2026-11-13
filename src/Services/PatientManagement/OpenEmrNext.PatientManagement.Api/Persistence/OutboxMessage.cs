namespace OpenEmrNext.PatientManagement.Api.Persistence;

/// <summary>
/// One row of the transactional outbox (ADR 0049): written in the same transaction as the business
/// change it records, inside the same <c>patient_management_outbox</c> table in the shared <c>openemr</c>
/// database -- not a separate store, so the business write and this row commit atomically together.
/// What relays these rows onward is a deliberately separate concern, not decided here.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; private set; }
    public string EventType { get; }
    public string Payload { get; }
    public DateTime OccurredAtUtc { get; }
    public DateTime? PublishedAtUtc { get; private set; }

    private OutboxMessage(string eventType, string payload, DateTime occurredAtUtc)
    {
        EventType = eventType;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public static OutboxMessage For(string eventType, string payload, DateTime occurredAtUtc) =>
        new(eventType, payload, occurredAtUtc);
}
