using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Admin;

/// <summary>
/// A pool after a change.
/// </summary>
/// <param name="Id">The pool's id.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone.</param>
/// <param name="HostRef">The host.</param>
/// <param name="AdminState">The administrative state.</param>
public sealed record PoolResponse(int Id, string Region, string? Zone, string? HostRef, PoolAdminState AdminState);

/// <summary>
/// A pool's state after a change.
/// </summary>
/// <param name="Id">The pool's id.</param>
/// <param name="AdminState">The administrative state.</param>
public sealed record PoolStateResponse(int Id, PoolAdminState AdminState);

/// <summary>
/// What an inventory change did.
/// </summary>
/// <param name="Applied">Whether the configuration was applied.</param>
/// <param name="PoolTakenOffline">Whether a forced change took the pool offline instead.</param>
/// <param name="Held">Units held at the time.</param>
/// <param name="Used">Units allocated at the time.</param>
public sealed record InventoryChangeResponse(bool Applied, bool PoolTakenOffline, long Held, long Used);

/// <summary>
/// A product after a change.
/// </summary>
/// <param name="Code">The product code.</param>
/// <param name="Status">Active or Retired.</param>
/// <param name="ResourceClasses">The classes a request may carry.</param>
/// <param name="HoldTtlSeconds">The lease length of a hold.</param>
/// <param name="WaitMaxSeconds">The longest a request may wait.</param>
/// <param name="MaxActiveHolds">Open holds one customer may have.</param>
/// <param name="MaxWaiting">Waiting requests one customer may have.</param>
/// <param name="MaxPoolSharePct">The share of a pool one customer may hold.</param>
public sealed record ProductResponse(
    string Code, ProductStatus Status, IReadOnlyList<ResourceClass> ResourceClasses, int HoldTtlSeconds,
    int WaitMaxSeconds, int MaxActiveHolds, int MaxWaiting, int MaxPoolSharePct);

/// <summary>
/// A quota after a change.
/// </summary>
/// <param name="CustomerId">The customer.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Region">The region.</param>
/// <param name="Limit">The limit.</param>
public sealed record QuotaResponse(string CustomerId, ResourceClass ResourceClass, string Region, long Limit);

/// <summary>
/// The result of cancelling a customer's open reservations.
/// </summary>
/// <param name="Cancelled">How many holds and waiting requests were cancelled.</param>
public sealed record CancelOpenResponse(int Cancelled);

/// <summary>
/// The reconciliation report.
/// </summary>
/// <param name="GeneratedAt">When it was computed.</param>
/// <param name="DriftRows">The number of drifting rows; must be 0.</param>
/// <param name="Drift">Each drifting counter with expected and actual values.</param>
/// <param name="UnsettledHolds">Holds past their lease but not yet stored as expired.</param>
/// <param name="UnsettledWaiting">Waits past their deadline but not yet stored as rejected.</param>
/// <param name="OutboxBacklog">Events not yet published.</param>
/// <param name="OutboxOldestAgeSeconds">Age of the oldest unpublished event.</param>
public sealed record ReconciliationResponse(
    DateTimeOffset GeneratedAt, int DriftRows, IReadOnlyList<DriftRowResponse> Drift, long UnsettledHolds,
    long UnsettledWaiting, long OutboxBacklog, long OutboxOldestAgeSeconds);

/// <summary>
/// One counter that disagrees with what it should equal.
/// </summary>
/// <param name="Check">Which comparison found it.</param>
/// <param name="Key">The row.</param>
/// <param name="Counter"><c>held</c> or <c>used</c>.</param>
/// <param name="Expected">What the ledger or the reservations say.</param>
/// <param name="Actual">What the counter says.</param>
public sealed record DriftRowResponse(string Check, string Key, string Counter, long Expected, long Actual);
