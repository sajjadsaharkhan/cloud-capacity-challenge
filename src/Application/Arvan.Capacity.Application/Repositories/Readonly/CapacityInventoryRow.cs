using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// One inventory as the capacity reads see it, with the units held by reservations whose lease has
/// already ended counted back as free.
/// </summary>
/// <param name="Region">The pool's region.</param>
/// <param name="PoolId">The pool's id.</param>
/// <param name="Zone">The pool's zone.</param>
/// <param name="HostRef">The pool's host, for a host-scoped pool.</param>
/// <param name="ResourceClass">The class counted.</param>
/// <param name="Sellable">What can be sold from the inventory.</param>
/// <param name="Held">Units held, as stored.</param>
/// <param name="Used">Units allocated.</param>
/// <param name="ExpiredHeld">Held units whose reservation is past its lease but not yet stored as expired.</param>
public sealed record CapacityInventoryRow(
    string Region,
    int PoolId,
    string? Zone,
    string? HostRef,
    ResourceClass ResourceClass,
    long Sellable,
    long Held,
    long Used,
    long ExpiredHeld)
{
    /// <summary>
    /// Units a new hold could take: sellable minus held and used, plus what has expired unsettled.
    /// </summary>
    public long EffectiveFree => Math.Max(0, Sellable - Held - Used + ExpiredHeld);
}
