using Arvan.Capacity.Application.Repositories;

namespace Arvan.Capacity.Application.Clients;

/// <summary>
/// Publishes events to the broker. Used only by the relay, after commit; the broker is never on
/// the decision path.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes every event and returns once the broker confirmed all of them.
    /// </summary>
    /// <param name="events">The events to publish, in order.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task PublishAsync(IReadOnlyList<OutboxEvent> events, CancellationToken ct);
}
