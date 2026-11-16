using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Events;

namespace Arvan.Capacity.Application.UseCases.Events.RelayEvents;

/// <summary>
/// Publishes the events the transitions left in the outbox. Triggered by the relay every 200 ms;
/// several relays are safe because locked rows are skipped.
/// </summary>
public sealed class RelayEventsUseCase(ITransactionManager transactionManager, IOutboxOperations outbox)
    : IUseCase<RelayEventsInput, RelayEventsOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public async Task<RelayEventsOutput> ExecuteAsync(RelayEventsInput input, CancellationToken ct) =>
        new(await transactionManager.RunAsync(token => outbox.RelayAsync(input.BatchSize, token), ct));
}
