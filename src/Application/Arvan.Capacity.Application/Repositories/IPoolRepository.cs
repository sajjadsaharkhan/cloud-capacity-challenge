using Arvan.Capacity.Domain.Entities.Capacity;

namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The write-side port for <see cref="Pool"/> and the configuration of its inventories.
/// </summary>
public interface IPoolRepository : IRepository<Pool>
{
    /// <summary>
    /// Finds a pool and locks its row, or returns <c>null</c> if there is none.
    /// </summary>
    /// <param name="id">The pool's id.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Pool?> FindForUpdateAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stores a pool's location and administrative state.
    /// </summary>
    /// <param name="pool">The pool after its change.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task UpdateAsync(Pool pool, CancellationToken ct);

    /// <summary>
    /// Whether any inventory of the pool still has held or used units.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<bool> HasHeldOrUsedUnitsAsync(int poolId, CancellationToken ct);

    /// <summary>
    /// Finds one inventory of a pool and locks its row, or returns <c>null</c> if there is none.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<InventorySnapshot?> FindInventoryForUpdateAsync(int poolId, Domain.Enums.ResourceClass resourceClass, CancellationToken ct);

    /// <summary>
    /// Creates or updates the configuration of one inventory. The counters are not touched.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="configuration">The configuration to store.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task UpsertInventoryAsync(int poolId, Domain.Enums.ResourceClass resourceClass, InventoryConfiguration configuration, CancellationToken ct);
}
