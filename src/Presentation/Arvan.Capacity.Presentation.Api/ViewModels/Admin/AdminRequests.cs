using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Admin;

/// <summary>
/// The JSON body for <c>PUT /v1/admin/pools/{id}</c>.
/// </summary>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone; omitted for a region-scoped pool.</param>
/// <param name="HostRef">The host; only for a host-scoped pool, which needs a zone.</param>
public sealed record PutPoolRequest(string Region, string? Zone, string? HostRef);

/// <summary>
/// The JSON body for <c>POST /v1/admin/pools/{id}/state</c>.
/// </summary>
/// <param name="Event">One of <c>activate</c>, <c>take_offline</c>, <c>restore</c>, <c>retire</c>.</param>
public sealed record ChangePoolStateRequest(string Event);

/// <summary>
/// The JSON body for <c>PUT /v1/admin/pools/{id}/inventories/{class}</c>.
/// </summary>
/// <param name="Total">The physical total.</param>
/// <param name="Reserved">The fragmentation margin kept out of sale.</param>
/// <param name="AllocationRatio">The overcommit ratio, at least 1.</param>
/// <param name="MinUnit">The smallest quantity one line may ask for.</param>
/// <param name="StepSize">The quantity a line must be a multiple of.</param>
/// <param name="MaxUnit">The largest quantity one line may ask for.</param>
/// <param name="Force">When the change would leave less to sell than is in use: take the pool offline instead of refusing.</param>
public sealed record PutInventoryRequest(
    long Total, long Reserved, decimal AllocationRatio, long MinUnit, long StepSize, long MaxUnit, bool Force);

/// <summary>
/// The JSON body for <c>PUT /v1/admin/products/{code}</c>.
/// </summary>
/// <param name="Status">Active or Retired.</param>
/// <param name="ResourceClasses">The classes a request may carry.</param>
/// <param name="HoldTtlSeconds">The lease length of a hold, 120 to 3600.</param>
/// <param name="WaitMaxSeconds">The longest a request may wait, up to 900.</param>
/// <param name="MaxActiveHolds">Open holds one customer may have.</param>
/// <param name="MaxWaiting">Waiting requests one customer may have.</param>
/// <param name="MaxPoolSharePct">The share of a pool one customer may hold, 1 to 100.</param>
public sealed record PutProductRequest(
    ProductStatus Status, IReadOnlyList<ResourceClass> ResourceClasses, int HoldTtlSeconds, int WaitMaxSeconds,
    int MaxActiveHolds, int MaxWaiting, int MaxPoolSharePct);

/// <summary>
/// The JSON body for <c>PUT /v1/admin/quotas/{customer}/{class}/{region}</c>.
/// </summary>
/// <param name="Limit">The contract limit.</param>
public sealed record PutQuotaRequest(long Limit);

/// <summary>
/// The JSON body for <c>POST /v1/admin/ledger/adjustments</c>: a pool, or a customer and region.
/// </summary>
/// <param name="PoolId">The pool, for an inventory correction.</param>
/// <param name="CustomerId">The customer, for a quota correction.</param>
/// <param name="Region">The region, for a quota correction.</param>
/// <param name="ResourceClass">The class corrected.</param>
/// <param name="DeltaHeld">The change to <c>held</c>.</param>
/// <param name="DeltaUsed">The change to <c>used</c>.</param>
/// <param name="Reason">Why; required.</param>
public sealed record AdjustLedgerRequest(
    int? PoolId, string? CustomerId, string? Region, ResourceClass ResourceClass, long DeltaHeld, long DeltaUsed, string Reason);
