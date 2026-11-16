using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Pools;

/// <summary>
/// Owns validation and write-orchestration for <see cref="Pool"/> and the configuration of its
/// inventories. Never begins or commits a transaction, never calls a peer Operation.
/// </summary>
public interface IPoolOperations
{
    /// <summary>
    /// Creates a pool, registered and not yet sold, or moves an existing one.
    /// </summary>
    /// <param name="id">The pool's id.</param>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone, if any.</param>
    /// <param name="hostRef">The host, for a host-scoped pool.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Pool> PutAsync(int id, string region, string? zone, string? hostRef, CancellationToken ct);

    /// <summary>
    /// Applies a state change to a pool, following the resource state machine.
    /// </summary>
    /// <param name="id">The pool's id.</param>
    /// <param name="stateEvent">One of <c>activate</c>, <c>take_offline</c>, <c>restore</c>, <c>retire</c>.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="NotFoundException">Thrown when no such pool exists.</exception>
    /// <exception cref="Domain.Exceptions.DomainException">Thrown when the change is not legal from the pool's state.</exception>
    Task<Pool> ChangeStateAsync(int id, string stateEvent, CancellationToken ct);

    /// <summary>
    /// Sets the configuration of one inventory of a pool.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="force">When the change would leave less to sell than is in use: take the pool offline instead of applying it.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="NotFoundException">Thrown when the pool does not exist.</exception>
    /// <exception cref="Domain.Exceptions.DomainException">Thrown when the change is below usage and <paramref name="force"/> is not set.</exception>
    Task<InventoryChange> SetInventoryAsync(
        int poolId, ResourceClass resourceClass, InventoryConfiguration configuration, bool force, CancellationToken ct);
}

/// <summary>
/// What an inventory change did.
/// </summary>
/// <param name="Applied">Whether the configuration was applied.</param>
/// <param name="PoolTakenOffline">Whether a forced change took the pool offline instead of being applied.</param>
/// <param name="Held">Units held at the time.</param>
/// <param name="Used">Units allocated at the time.</param>
public sealed record InventoryChange(bool Applied, bool PoolTakenOffline, long Held, long Used);
