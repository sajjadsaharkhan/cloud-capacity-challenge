using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Capacity;

/// <summary>
/// The answer of <c>POST /v1/capacity/fit-check</c>. Advisory only; it never states how much is free.
/// </summary>
/// <param name="Fits">Whether a submit now would be held.</param>
/// <param name="Code">The reason it would not.</param>
/// <param name="Lines">The reason per short line.</param>
public sealed record FitCheckResponse(bool Fits, ReasonCode? Code, IReadOnlyList<FitCheckLineResponse> Lines);

/// <summary>
/// One short line of a <see cref="FitCheckResponse"/>.
/// </summary>
/// <param name="ResourceClass">The class.</param>
/// <param name="Code">Why it would not be held.</param>
public sealed record FitCheckLineResponse(ResourceClass ResourceClass, ReasonCode Code);

/// <summary>
/// The answer of <c>GET /v1/capacity</c>.
/// </summary>
/// <param name="Items">One level per product and region.</param>
/// <param name="AsOf">When the levels were read.</param>
/// <param name="Stale">Whether the answer comes from a cache because the database is unreachable.</param>
/// <param name="Pools">Per-pool numbers, only for operators and internal services that asked for detail.</param>
public sealed record CapacityLevelsResponse(
    IReadOnlyList<CapacityLevelResponse> Items, DateTimeOffset AsOf, bool Stale, IReadOnlyList<PoolCapacityResponse>? Pools);

/// <summary>
/// The level of one product in one region.
/// </summary>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Level">Unavailable, Limited or Available.</param>
public sealed record CapacityLevelResponse(string Product, string Region, CapacityLevel Level);

/// <summary>
/// The numbers of one inventory.
/// </summary>
/// <param name="Region">The region.</param>
/// <param name="PoolId">The pool.</param>
/// <param name="Zone">The zone.</param>
/// <param name="HostRef">The host.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Sellable">What can be sold.</param>
/// <param name="Held">Units held.</param>
/// <param name="Used">Units allocated.</param>
/// <param name="Free">Effective free units.</param>
public sealed record PoolCapacityResponse(
    string Region, int PoolId, string? Zone, string? HostRef, ResourceClass ResourceClass,
    long Sellable, long Held, long Used, long Free);
