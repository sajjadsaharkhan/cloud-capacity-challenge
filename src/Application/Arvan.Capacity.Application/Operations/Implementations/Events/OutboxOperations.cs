using Arvan.Capacity.Application.Clients;
using Arvan.Capacity.Application.Operations.Events;
using Arvan.Capacity.Application.Repositories;

namespace Arvan.Capacity.Application.Operations.Implementations.Events;

/// <inheritdoc cref="IOutboxOperations"/>
public sealed class OutboxOperations(IOutboxRepository outbox, IEventPublisher publisher) : IOutboxOperations
{
    /// <inheritdoc cref="IOutboxOperations.RelayAsync"/>
    public async Task<int> RelayAsync(int batchSize, CancellationToken ct)
    {
        var events = await outbox.LockUnpublishedAsync(batchSize, ct);
        if (events.Count is 0)
            return 0;

        await publisher.PublishAsync(events, ct);
        await outbox.MarkPublishedAsync([.. events.Select(e => e.Id)], ct);
        return events.Count;
    }
}
