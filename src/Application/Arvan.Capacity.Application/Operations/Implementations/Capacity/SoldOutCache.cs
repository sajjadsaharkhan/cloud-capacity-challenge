using System.Collections.Concurrent;
using System.Diagnostics;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Implementations.Capacity;

/// <summary>
/// A one-second, per-process memory of inventories that were just found sold out: for each
/// (pool, class), the most a request could ever get while live allocations stay as they are,
/// <c>sellable − used</c>. A request asking for more skips the hot-row <c>UPDATE</c> and goes
/// straight to classification, so a burst against a sold-out pool does not queue on its row lock.
/// </summary>
/// <remarks>
/// Only a shortage that retrying cannot soon fix is remembered. <c>CAPACITY_TEMPORARILY_UNAVAILABLE</c>
/// never is, because holds expire. The cache is cleared by anything in this process that gives
/// capacity back or changes an inventory; another instance sees the change within the second.
/// </remarks>
public sealed class SoldOutCache
{
    private static readonly long Lifetime = Stopwatch.Frequency; // one second
    private readonly ConcurrentDictionary<(int PoolId, ResourceClass ResourceClass), (long Max, long Until)> _entries = new();

    /// <summary>
    /// Remembers that an inventory cannot give more than <paramref name="max"/> for the next second.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="max">The most a request could get: sellable minus used.</param>
    public void Remember(int poolId, ResourceClass resourceClass, long max) =>
        _entries[(poolId, resourceClass)] = (max, Stopwatch.GetTimestamp() + Lifetime);

    /// <summary>
    /// Whether a request for <paramref name="quantity"/> is known to be too big for the inventory.
    /// </summary>
    /// <param name="poolId">The pool.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="quantity">The quantity asked for.</param>
    public bool IsSoldOut(int poolId, ResourceClass resourceClass, long quantity) =>
        _entries.TryGetValue((poolId, resourceClass), out var entry)
        && Stopwatch.GetTimestamp() < entry.Until
        && quantity > entry.Max;

    /// <summary>
    /// Forgets everything: capacity came back, or an inventory changed.
    /// </summary>
    public void Clear() => _entries.Clear();
}
