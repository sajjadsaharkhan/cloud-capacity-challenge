using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Implementations.Capacity;
using Arvan.Capacity.Application.Operations.Pools;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;

namespace Arvan.Capacity.Application.Operations.Implementations.Pools;

/// <inheritdoc cref="IPoolOperations"/>
public sealed class PoolOperations(IPoolRepository pools, SoldOutCache soldOut) : IPoolOperations
{
    /// <inheritdoc cref="IPoolOperations.PutAsync"/>
    public async Task<Pool> PutAsync(int id, string region, string? zone, string? hostRef, CancellationToken ct)
    {
        var pool = await pools.FindForUpdateAsync(id, ct);

        if (pool is null)
        {
            pool = Pool.Register(id, region, zone, hostRef);
            await pools.AddAsync(pool, ct);
        }
        else
        {
            pool.Relocate(region, zone, hostRef);
            await pools.UpdateAsync(pool, ct);
        }

        return pool;
    }

    /// <inheritdoc cref="IPoolOperations.ChangeStateAsync"/>
    public async Task<Pool> ChangeStateAsync(int id, string stateEvent, CancellationToken ct)
    {
        var pool = await pools.FindForUpdateAsync(id, ct) ?? throw new EntityNotFoundException<Pool>(id);

        switch (stateEvent)
        {
            case "activate": pool.Activate(); break;
            case "take_offline": pool.TakeOffline(); break;
            case "restore": pool.Restore(); break;
            case "retire": pool.Retire(await pools.HasHeldOrUsedUnitsAsync(id, ct)); break;
            default: throw new PoolInvalidTransitionException(id, pool.AdminState, stateEvent);
        }

        await pools.UpdateAsync(pool, ct);
        return pool;
    }

    /// <inheritdoc cref="IPoolOperations.SetInventoryAsync"/>
    public async Task<InventoryChange> SetInventoryAsync(
        int poolId, ResourceClass resourceClass, InventoryConfiguration configuration, bool force, CancellationToken ct)
    {
        var pool = await pools.FindForUpdateAsync(poolId, ct) ?? throw new EntityNotFoundException<Pool>(poolId);
        configuration.EnsureValid();

        var current = await pools.FindInventoryForUpdateAsync(poolId, resourceClass, ct);
        var held = current?.Held ?? 0;
        var used = current?.Used ?? 0;

        if (configuration.IsBelowUsage(held, used))
        {
            if (!force)
                throw new InventoryBelowUsageException(poolId, resourceClass, held, used);

            // Not applied: keeping a pending value would need extra state for a rare operation.
            // Taking the pool offline stops new holds landing on it; the operator repeats the update
            // once the units fit.
            var offline = pool.AdminState is PoolAdminState.Available;
            if (offline)
            {
                pool.TakeOffline();
                await pools.UpdateAsync(pool, ct);
            }

            return new InventoryChange(Applied: false, PoolTakenOffline: offline, held, used);
        }

        await pools.UpsertInventoryAsync(poolId, resourceClass, configuration, ct);
        soldOut.Clear();
        return new InventoryChange(Applied: true, PoolTakenOffline: false, held, used);
    }
}
