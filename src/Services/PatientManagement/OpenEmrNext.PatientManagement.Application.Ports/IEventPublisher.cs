namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// Delivers one event to whoever consumes it. The transport is behind this port (EVT-8, KD-4): today an HTTP push, and a
/// message broker can replace the adapter without touching the relay or any caller. Delivery is at-least-once, so a
/// consumer must be idempotent on <see cref="OutboundEvent.EventId"/>.
/// </summary>
public interface IEventPublisher
{
    /// <exception cref="EventPublishException">The event was not delivered. <see cref="EventPublishException.IsPermanent"/> says whether retrying can help.</exception>
    Task PublishAsync(OutboundEvent @event, CancellationToken ct);
}

/// <summary>Delivery failed. <c>IsPermanent</c> is true when the receiver rejected the event itself (retrying the same bytes cannot succeed), false when the problem is the path to it (down, slow, overloaded).</summary>
public sealed class EventPublishException(string message, bool isPermanent, Exception? inner = null)
    : Exception(message, inner)
{
    public bool IsPermanent { get; } = isPermanent;
}
