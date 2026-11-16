namespace Arvan.Capacity.Application.Operations.Events;

/// <summary>
/// Owns the transactional outbox: it hands unpublished events to the broker and records that it
/// did. Never begins or commits a transaction, never calls a peer Operation.
/// </summary>
public interface IOutboxOperations
{
    /// <summary>
    /// Locks a batch of unpublished events, publishes them with confirmation, and marks them
    /// published. If the process dies between publishing and the commit, the batch is published
    /// again, which is why delivery is at least once.
    /// </summary>
    /// <param name="batchSize">The largest number of events to relay.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns>The number of events relayed.</returns>
    Task<int> RelayAsync(int batchSize, CancellationToken ct);
}
