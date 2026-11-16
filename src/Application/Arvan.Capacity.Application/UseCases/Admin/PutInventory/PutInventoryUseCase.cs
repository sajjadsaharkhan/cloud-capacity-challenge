using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Pools;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutInventory;

/// <summary>
/// Sets the size, margin, overcommit and quantity rules of an inventory. Corresponds to
/// <c>PUT /v1/admin/pools/{id}/inventories/{class}</c>.
/// </summary>
public sealed class PutInventoryUseCase(
    ITransactionManager transactionManager, IIdempotencyOperations idempotency, IPoolOperations pools)
    : IUseCase<PutInventoryInput, PutInventoryOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public Task<PutInventoryOutput> ExecuteAsync(PutInventoryInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            if (input.Actor.Kind is not ActorKind.Admin)
                throw new ActorNotAllowedException("change inventories");

            var claim = await idempotency.ClaimAsync<PutInventoryOutput>(
                input.Actor.Id, input.IdempotencyKey, $"admin.put_inventory:{input.PoolId}:{input.ResourceClass}",
                new { input.PoolId, input.ResourceClass, input.Configuration, input.Force }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            var change = await pools.SetInventoryAsync(input.PoolId, input.ResourceClass, input.Configuration, input.Force, token);

            var output = new PutInventoryOutput(change.Applied, change.PoolTakenOffline, change.Held, change.Used, Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
