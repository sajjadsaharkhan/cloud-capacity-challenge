using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Reservations;

namespace Arvan.Capacity.Application.Operations.Implementations.Capacity;

/// <inheritdoc cref="ICapacityOperations"/>
public sealed class CapacityOperations(ICapacityLedgerRepository ledger, SoldOutCache soldOut) : ICapacityOperations
{
    /// <inheritdoc cref="ICapacityOperations.IsKnownSoldOut"/>
    public bool IsKnownSoldOut(HoldPlan plan) =>
        plan.Lines.Any(l => soldOut.IsSoldOut(l.PoolId, l.ResourceClass, l.Quantity));

    /// <inheritdoc cref="ICapacityOperations.PlanHoldAsync"/>
    public async Task<CapacityPlanning> PlanHoldAsync(
        string region,
        string? zone,
        IReadOnlyList<ReservationLine> lines,
        CancellationToken ct)
    {
        var snapshots = await ledger.GetSnapshotsAsync(region, [.. lines.Select(l => l.ResourceClass)], ct);

        if (zone is not null && !snapshots.Any(s => s.Zone == zone))
            throw new ReservationZoneInvalidException(zone, region);

        // A class's limits are the same in every pool of the region, so the first pool speaks for it.
        foreach (var line in lines)
            snapshots.FirstOrDefault(s => s.ResourceClass == line.ResourceClass)?.EnsureQuantityAllowed(line.Quantity);

        return new CapacityPlanning(HoldPlanner.Plan(zone, lines, snapshots), snapshots);
    }

    /// <inheritdoc cref="ICapacityOperations.TryHoldAsync"/>
    public Task<HoldResult> TryHoldAsync(
        Guid reservationId, string customerId, string region, HoldPlan plan, Product product,
        DateTimeOffset now, CancellationToken ct) =>
        ledger.TryHoldAsync(new LedgerHold(reservationId, customerId, region, plan.Lines, product.MaxPoolSharePct), now, ct);

    /// <inheritdoc cref="ICapacityOperations.ClassifyShortageAsync"/>
    public async Task<ShortageClassification> ClassifyShortageAsync(
        IReadOnlyList<InventorySnapshot> planned, string? zone, IReadOnlyList<ReservationLine> lines, DateTimeOffset now, CancellationToken ct)
    {
        var snapshots = planned
            .Where(s => zone is null || s.Zone is null || s.Zone == zone)
            .ToList();

        // A line that some available pool could take on its own is not what made the request fail.
        var shortCodes = lines
            .Select(line => (Line: line, Pools: snapshots.Where(s => s.ResourceClass == line.ResourceClass).ToList()))
            .Where(x => !x.Pools.Any(p => p.Free >= x.Line.Quantity))
            .Select(x => ShortageClassifier.Classify(x.Line.Quantity, x.Pools))
            .ToList();

        var reason = shortCodes.Count is 0 ? ReasonCode.CapacityTemporarilyUnavailable : ShortageClassifier.MostSevere(shortCodes);
        if (reason is not ReasonCode.CapacityTemporarilyUnavailable)
        {
            foreach (var line in lines)
                foreach (var pool in snapshots.Where(s => s.ResourceClass == line.ResourceClass))
                    soldOut.Remember(pool.PoolId, pool.ResourceClass, pool.Sellable - pool.Used);

            return new ShortageClassification(reason, null);
        }

        // Capacity returns when the earliest hold on these pools ends; an offline pool says nothing.
        var poolIds = snapshots.Select(s => s.PoolId).Distinct().ToList();
        var offline = snapshots.Any(s => s.AdminState is PoolAdminState.Offline);
        var earliest = await ledger.GetEarliestHoldExpiryAsync(poolIds, ct);

        var seconds = offline || earliest is null
            ? 60
            : Math.Clamp((int)Math.Ceiling((earliest.Value - now).TotalSeconds), 1, 60);

        return new ShortageClassification(reason, seconds);
    }

    /// <inheritdoc cref="ICapacityOperations.AdjustAsync"/>
    public async Task AdjustAsync(LedgerAdjustment adjustment, DateTimeOffset now, CancellationToken ct)
    {
        await ledger.AdjustAsync(adjustment, now, ct);
        soldOut.Clear();
    }

    /// <inheritdoc cref="ICapacityOperations.CommitAsync"/>
    public Task CommitAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct) =>
        ledger.CommitAsync(reservation, now, ct);

    /// <inheritdoc cref="ICapacityOperations.ReturnAsync"/>
    public Task ReturnAsync(IReadOnlyCollection<Reservation> reservations, DateTimeOffset now, CancellationToken ct) =>
        reservations.Count is 0 ? Task.CompletedTask : ledger.ReturnAsync(reservations, now, ct);

    /// <inheritdoc cref="ICapacityOperations.ReleaseAsync"/>
    public async Task ReleaseAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        await ledger.ReleaseAsync(reservation, now, ct);
        soldOut.Clear();
    }
}
