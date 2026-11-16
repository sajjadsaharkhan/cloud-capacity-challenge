using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions;

namespace Arvan.Capacity.Application.Operations.Capacity;

/// <summary>
/// Owns planning and the counters for the capacity inventories. Never begins or commits a
/// transaction, never calls a peer Operation.
/// </summary>
public interface ICapacityOperations
{
    /// <summary>
    /// Validates a request's quantities and zone against the region's inventories and returns the
    /// plans to attempt, best first. The snapshot is read without locks and only ranks.
    /// </summary>
    /// <param name="region">The region requested.</param>
    /// <param name="zone">The zone the client named, if any.</param>
    /// <param name="lines">The lines requested.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="DomainValidationException">Thrown when the zone is not in the region or a quantity breaks its class's limits.</exception>
    Task<CapacityPlanning> PlanHoldAsync(
        string region,
        string? zone,
        IReadOnlyList<ReservationLine> lines,
        CancellationToken ct);

    /// <summary>
    /// Whether a plan is known to be hopeless: one of its inventories was found sold out in the
    /// last second and cannot give the quantity asked for. Such a plan skips the hot-row update.
    /// </summary>
    /// <param name="plan">The plan to judge.</param>
    bool IsKnownSoldOut(HoldPlan plan);

    /// <summary>
    /// Takes a reservation's lines out of quota and free capacity, all or nothing, under the
    /// caller's savepoint.
    /// </summary>
    /// <param name="reservationId">The inserted reservation.</param>
    /// <param name="customerId">The customer whose quota is charged.</param>
    /// <param name="region">The region.</param>
    /// <param name="plan">The pools to take from.</param>
    /// <param name="product">The product, whose share cap applies.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<HoldResult> TryHoldAsync(
        Guid reservationId, string customerId, string region, HoldPlan plan, Product product,
        DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Explains why a request that no plan could hold was refused, and when retrying could help.
    /// </summary>
    /// <param name="snapshots">The inventories read when the request was planned; nothing is read again.</param>
    /// <param name="zone">The zone the client named; only pools in it (and region-scoped pools) are in scope. <c>null</c> for the whole region.</param>
    /// <param name="lines">The lines requested.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ShortageClassification> ClassifyShortageAsync(
        IReadOnlyList<InventorySnapshot> snapshots, string? zone, IReadOnlyList<ReservationLine> lines, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Applies an operator's correction to a counter.
    /// </summary>
    /// <param name="adjustment">The correction.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task AdjustAsync(LedgerAdjustment adjustment, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Moves a confirmed reservation's quantities from held to used.
    /// </summary>
    /// <param name="reservation">The confirmed reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task CommitAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Gives the held quantities of cancelled or expired reservations back to free capacity.
    /// </summary>
    /// <param name="reservations">The reservations that left the held state.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ReturnAsync(IReadOnlyCollection<Reservation> reservations, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Gives a released reservation's used quantities back to free capacity.
    /// </summary>
    /// <param name="reservation">The released reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ReleaseAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// Why a request was short, and when retrying could help.
/// </summary>
/// <param name="Reason">The most severe shortage code across the short lines.</param>
/// <param name="RetryAfterSeconds">For a temporary shortage, seconds until capacity next returns, clamped to 1-60; otherwise <c>null</c>.</param>
public sealed record ShortageClassification(ReasonCode Reason, int? RetryAfterSeconds);

/// <summary>
/// Where a request can be attempted, and the capacity the plans were ranked from.
/// </summary>
/// <param name="Plans">The plans to attempt, best first.</param>
/// <param name="Snapshots">The inventories of the region for the requested classes, read in the open transaction.</param>
public sealed record CapacityPlanning(IReadOnlyList<HoldPlan> Plans, IReadOnlyList<InventorySnapshot> Snapshots)
{
    /// <summary>
    /// Whether every plan is already known to fail: in each one, some line asks for more than its
    /// pool has free. Attempting such a request would only take locks to be refused, so the caller
    /// goes straight to classifying it.
    /// </summary>
    /// <remarks>
    /// Safe by construction: only a conditional <c>UPDATE</c> ever grants capacity, so this can
    /// refuse a request a returning hold could have served a moment later, but never grant one it
    /// should not.
    /// </remarks>
    public bool IsHopeless =>
        Plans.All(plan => plan.Lines.Any(line =>
            Snapshots.FirstOrDefault(s => s.PoolId == line.PoolId && s.ResourceClass == line.ResourceClass) is not { } pool
            || pool.Free < line.Quantity));
}
