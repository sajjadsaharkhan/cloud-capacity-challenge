using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Idempotency;

namespace Arvan.Capacity.Application.UseCases.Reservations.PurgeIdempotencyKeys;

/// <summary>
/// Deletes idempotency keys past their 24-hour retention. Triggered by the sweeper.
/// </summary>
public sealed class PurgeIdempotencyKeysUseCase(ITransactionManager transactionManager, IIdempotencyOperations idempotency)
    : IUseCase<PurgeIdempotencyKeysInput, PurgeIdempotencyKeysOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public async Task<PurgeIdempotencyKeysOutput> ExecuteAsync(PurgeIdempotencyKeysInput input, CancellationToken ct) =>
        new(await transactionManager.RunAsync(token => idempotency.PurgeExpiredAsync(input.BatchSize, token), ct));
}
