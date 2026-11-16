using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;
using Arvan.Capacity.Framework.Domain.Abstractions;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// The unit of capacity: the administrative half of the resource state machine. Its
/// <see cref="AdminState"/> decides where a pool's idle units sit; held and allocated units finish
/// their own cycle whatever happens here.
/// </summary>
public sealed class Pool : BaseEntity<int>
{
    /// <summary>
    /// The region the pool belongs to.
    /// </summary>
    public string Region { get; private set; } = default!;

    /// <summary>
    /// The zone, or <c>null</c> for a region-scoped pool.
    /// </summary>
    public string? Zone { get; private set; }

    /// <summary>
    /// The host a host-scoped pool (GPUs) stands for.
    /// </summary>
    public string? HostRef { get; private set; }

    /// <summary>
    /// The administrative state.
    /// </summary>
    public PoolAdminState AdminState { get; private set; }

    /// <summary>
    /// Reserved for materialization by the persistence layer.
    /// </summary>
    private Pool()
    {
    }

    /// <summary>
    /// Describes a pool: registered, and not yet sold.
    /// </summary>
    /// <param name="id">The pool's id; chosen by the operator.</param>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone, if any.</param>
    /// <param name="hostRef">The host, for a host-scoped pool.</param>
    /// <exception cref="InvalidPoolException">Thrown when the region is blank or a host has no zone.</exception>
    public static Pool Register(int id, string region, string? zone, string? hostRef)
    {
        if (string.IsNullOrWhiteSpace(region) || (hostRef is not null && zone is null))
            throw new InvalidPoolException();

        var pool = new Pool { Region = region, Zone = zone, HostRef = hostRef, AdminState = PoolAdminState.Registered };
        pool.Id = id;
        return pool;
    }

    /// <summary>
    /// Changes where the pool is. The administrative state is untouched.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone, if any.</param>
    /// <param name="hostRef">The host, for a host-scoped pool.</param>
    /// <exception cref="InvalidPoolException">Thrown when the region is blank or a host has no zone.</exception>
    public void Relocate(string region, string? zone, string? hostRef)
    {
        if (string.IsNullOrWhiteSpace(region) || (hostRef is not null && zone is null))
            throw new InvalidPoolException();

        Region = region;
        Zone = zone;
        HostRef = hostRef;
    }

    /// <summary>
    /// <c>REGISTERED → AVAILABLE</c>: the pool becomes sellable.
    /// </summary>
    /// <exception cref="PoolInvalidTransitionException">Thrown when the pool is not registered.</exception>
    public void Activate() => Move(PoolAdminState.Registered, PoolAdminState.Available, "activate");

    /// <summary>
    /// <c>AVAILABLE → OFFLINE</c>: no new holds; existing holds and allocations finish normally.
    /// </summary>
    /// <exception cref="PoolInvalidTransitionException">Thrown when the pool is not available.</exception>
    public void TakeOffline() => Move(PoolAdminState.Available, PoolAdminState.Offline, "take_offline");

    /// <summary>
    /// <c>OFFLINE → AVAILABLE</c>: sellable again.
    /// </summary>
    /// <exception cref="PoolInvalidTransitionException">Thrown when the pool is not offline.</exception>
    public void Restore() => Move(PoolAdminState.Offline, PoolAdminState.Available, "restore");

    /// <summary>
    /// Retires the pool for good: from registered, or from offline once nothing is held or allocated.
    /// </summary>
    /// <param name="hasHeldOrUsedUnits">Whether any inventory of the pool still has held or used units.</param>
    /// <exception cref="PoolInvalidTransitionException">Thrown from any other state, or while units are still held or used.</exception>
    public void Retire(bool hasHeldOrUsedUnits)
    {
        if (AdminState is PoolAdminState.Registered || (AdminState is PoolAdminState.Offline && !hasHeldOrUsedUnits))
            AdminState = PoolAdminState.Retired;
        else
            throw new PoolInvalidTransitionException(Id, AdminState, "retire");
    }

    private void Move(PoolAdminState from, PoolAdminState to, string attempted)
    {
        if (AdminState != from)
            throw new PoolInvalidTransitionException(Id, AdminState, attempted);

        AdminState = to;
    }
}
