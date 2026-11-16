using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Flows.Reservations;

namespace Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;

/// <summary>
/// Stores the end of every hold whose lease ran out and returns its capacity. Triggered by the
/// sweeper every second; several sweepers are safe because locked rows are skipped.
/// </summary>
public sealed class ExpireHoldsUseCase(ITransactionManager transactionManager, ExpireHeldFlow expireHeld)
    : IUseCase<ExpireHoldsInput, ExpireHoldsOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public async Task<ExpireHoldsOutput> ExecuteAsync(ExpireHoldsInput input, CancellationToken ct) =>
        new(await transactionManager.RunAsync(
            token => expireHeld.ExecuteAsync(new ExpireHeldFlowInput(input.BatchSize, PoolIds: null), token),
            ct));
}
