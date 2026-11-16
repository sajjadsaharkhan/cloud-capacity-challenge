using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;

/// <summary>
/// The output of <see cref="GetCapacityLevelsUseCase"/>.
/// </summary>
/// <param name="Items">One level per product and region.</param>
/// <param name="AsOf">The database time the levels were read.</param>
/// <param name="Stale">Whether the answer comes from a cache because the database is unreachable.</param>
/// <param name="Pools">Per-pool numbers, only when detail was asked for and allowed.</param>
public sealed record GetCapacityLevelsOutput(
    IReadOnlyList<CapacityLevelOutput> Items,
    DateTimeOffset AsOf,
    bool Stale,
    IReadOnlyList<PoolCapacityOutput>? Pools);

/// <summary>
/// The level of one product in one region.
/// </summary>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Level">The worst level across the product's classes.</param>
public sealed record CapacityLevelOutput(string Product, string Region, CapacityLevel Level);

/// <summary>
/// The numbers of one inventory, for operators and internal services only.
/// </summary>
/// <param name="Region">The region.</param>
/// <param name="PoolId">The pool.</param>
/// <param name="Zone">The zone.</param>
/// <param name="HostRef">The host, for a host-scoped pool.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Sellable">What can be sold.</param>
/// <param name="Held">Units held, as stored.</param>
/// <param name="Used">Units allocated.</param>
/// <param name="Free">Effective free units, counting expired-unsettled holds as free.</param>
public sealed record PoolCapacityOutput(
    string Region, int PoolId, string? Zone, string? HostRef, ResourceClass ResourceClass,
    long Sellable, long Held, long Used, long Free);
