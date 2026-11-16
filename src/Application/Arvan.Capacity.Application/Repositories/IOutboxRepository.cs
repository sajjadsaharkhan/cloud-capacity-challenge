namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The port for the transactional outbox: the unpublished rows of <c>reservation_events</c>.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Locks up to <paramref name="limit"/> unpublished events in order, skipping rows another
    /// relay already holds.
    /// </summary>
    /// <param name="limit">The largest number of events to lock.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<OutboxEvent>> LockUnpublishedAsync(int limit, CancellationToken ct);

    /// <summary>
    /// Marks events as published, once the broker confirmed them.
    /// </summary>
    /// <param name="ids">The ids of the published events.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task MarkPublishedAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
}

/// <summary>
/// One event waiting to be published.
/// </summary>
/// <param name="Id">The row id; the event's identity within its cell.</param>
/// <param name="RoutingKey">The routing key, such as <c>reservation.confirmed</c>.</param>
/// <param name="Payload">The JSON envelope without its <c>event_id</c>.</param>
public sealed record OutboxEvent(long Id, string RoutingKey, string Payload);
