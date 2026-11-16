using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// The counters of one pool's inventory of one resource class at the moment they were read, with
/// the rules that decide what can still be sold from them. Read without locks, so it only ranks
/// and advises; the decision is the conditional <c>UPDATE</c> that follows.
/// </summary>
/// <param name="PoolId">The pool's id.</param>
/// <param name="Zone">The pool's zone, or <c>null</c> for a region-scoped pool.</param>
/// <param name="HostRef">The host of a host-scoped pool, otherwise <c>null</c>.</param>
/// <param name="AdminState">The pool's administrative state.</param>
/// <param name="ResourceClass">The class counted.</param>
/// <param name="Total">The physical total.</param>
/// <param name="Reserved">The fragmentation margin kept out of sale.</param>
/// <param name="AllocationRatio">The overcommit ratio, at least 1.</param>
/// <param name="Held">Units held by open reservations.</param>
/// <param name="Used">Units allocated by confirmed reservations.</param>
/// <param name="MinUnit">The smallest quantity one line may ask for.</param>
/// <param name="StepSize">The quantity a line must be a multiple of.</param>
/// <param name="MaxUnit">The largest quantity one line may ask for.</param>
public sealed record InventorySnapshot(
    int PoolId,
    string? Zone,
    string? HostRef,
    PoolAdminState AdminState,
    ResourceClass ResourceClass,
    long Total,
    long Reserved,
    decimal AllocationRatio,
    long Held,
    long Used,
    long MinUnit,
    long StepSize,
    long MaxUnit)
{
    /// <summary>
    /// What can be sold from this inventory: <c>floor((total − reserved) × allocation_ratio)</c>.
    /// The single definition the SQL mirrors; an integration test keeps the two in agreement.
    /// </summary>
    public long Sellable => ComputeSellable(Total, Reserved, AllocationRatio);

    /// <summary>
    /// Units a new hold could take right now: sellable minus held and used, and nothing at all
    /// unless the pool is <see cref="PoolAdminState.Available"/>.
    /// </summary>
    public long Free => AdminState is PoolAdminState.Available ? Math.Max(0, Sellable - Held - Used) : 0;

    /// <summary>
    /// Computes <c>floor((total − reserved) × ratio)</c>.
    /// </summary>
    /// <param name="total">The physical total.</param>
    /// <param name="reserved">The fragmentation margin.</param>
    /// <param name="allocationRatio">The overcommit ratio.</param>
    public static long ComputeSellable(long total, long reserved, decimal allocationRatio) =>
        (long)Math.Floor((total - reserved) * allocationRatio);

    /// <summary>
    /// Refuses a quantity that breaks this class's minimum, step size or maximum.
    /// </summary>
    /// <param name="quantity">The requested quantity.</param>
    /// <exception cref="QuantityNotAllowedException">Thrown when the quantity is not allowed.</exception>
    public void EnsureQuantityAllowed(long quantity)
    {
        if (quantity < MinUnit || quantity > MaxUnit || quantity % StepSize != 0)
            throw new QuantityNotAllowedException(ResourceClass, quantity);
    }
}
