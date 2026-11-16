using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Implementations.Reservations;

/// <inheritdoc cref="IReservationOperations"/>
public sealed class ReservationOperations(IReservationRepository reservations) : IReservationOperations
{
    /// <inheritdoc cref="IReservationOperations.GetForUpdateAsync"/>
    public async Task<Reservation> GetForUpdateAsync(Guid id, string customerId, CancellationToken ct) =>
        await reservations.FindForUpdateAsync(id, customerId, ct)
        ?? throw new EntityNotFoundException<Reservation>(id);

    /// <inheritdoc cref="IReservationOperations.EnsureClientReferenceAvailableAsync"/>
    public async Task EnsureClientReferenceAvailableAsync(string customerId, string? clientReference, CancellationToken ct)
    {
        if (clientReference is not null && await reservations.ClientReferenceInUseAsync(customerId, clientReference, ct))
            throw new ReservationClientReferenceTakenException(clientReference);
    }

    /// <inheritdoc cref="IReservationOperations.HoldAsync"/>
    public async Task<Reservation> HoldAsync(
        string customerId,
        Product product,
        string region,
        ShortagePolicy onShortage,
        string? clientReference,
        HoldPlan plan,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var reservation = Reservation.Hold(
            customerId,
            product.Id,
            region,
            plan.Zone,
            onShortage,
            clientReference,
            plan.Lines.Select(l => new ReservationLine(l.ResourceClass, l.Quantity, l.PoolId)),
            product.HoldTtl,
            actor,
            now);

        await reservations.AddAsync(reservation, ct);
        return reservation;
    }

    /// <inheritdoc cref="IReservationOperations.RejectAsync"/>
    public async Task<Reservation> RejectAsync(
        string customerId,
        string productCode,
        string region,
        ShortagePolicy onShortage,
        string? clientReference,
        IReadOnlyList<ReservationLine> lines,
        ReasonCode reason,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var reservation = Reservation.Reject(
            customerId, productCode, region, onShortage, clientReference, lines, reason, actor, now);

        await reservations.AddAsync(reservation, ct);
        return reservation;
    }

    /// <inheritdoc cref="IReservationOperations.ConfirmAsync"/>
    public async Task<bool> ConfirmAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct)
    {
        var changed = reservation.Confirm(now, actor);
        if (changed)
            await reservations.UpdateAsync(reservation, ct);

        return changed;
    }

    /// <inheritdoc cref="IReservationOperations.CancelAsync"/>
    public async Task<bool> CancelAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct)
    {
        var changed = reservation.Cancel(now, actor);
        if (changed)
            await reservations.UpdateAsync(reservation, ct);

        return changed;
    }

    /// <inheritdoc cref="IReservationOperations.ExtendAsync"/>
    public async Task ExtendAsync(
        Reservation reservation, DateTimeOffset now, TimeSpan holdTtl, Actor actor, CancellationToken ct)
    {
        reservation.Extend(now, holdTtl, actor);
        await reservations.UpdateAsync(reservation, ct);
    }

    /// <inheritdoc cref="IReservationOperations.ExpireAsync"/>
    public async Task ExpireAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        reservation.Expire(now, Actor.Sweeper);
        await reservations.UpdateAsync(reservation, ct);
    }

    /// <inheritdoc cref="IReservationOperations.ReleaseAsync"/>
    public async Task<bool> ReleaseAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct)
    {
        var changed = reservation.Release(now, actor);
        if (changed)
            await reservations.UpdateAsync(reservation, ct);

        return changed;
    }

    /// <inheritdoc cref="IReservationOperations.LockExpiredHeldAsync"/>
    public Task<IReadOnlyList<Reservation>> LockExpiredHeldAsync(
        int limit, IReadOnlyCollection<int>? poolIds, CancellationToken ct) =>
        reservations.LockExpiredHeldAsync(limit, poolIds, ct);

    /// <inheritdoc cref="IReservationOperations.ExpiredHeldExistsAsync"/>
    public Task<bool> ExpiredHeldExistsAsync(IReadOnlyCollection<int> poolIds, CancellationToken ct) =>
        reservations.ExpiredHeldExistsAsync(poolIds, ct);

    private const int CooldownThreshold = 5;
    private static readonly TimeSpan CooldownLength = TimeSpan.FromMinutes(10);

    /// <inheritdoc cref="IReservationOperations.EnsureWithinLimitsAsync"/>
    public async Task EnsureWithinLimitsAsync(
        string customerId, Product product, ShortagePolicy onShortage, DateTimeOffset now, CancellationToken ct)
    {
        var expiries = await reservations.GetRecentExpiriesAsync(customerId, product.Id, ct);
        if (expiries.CountLastHour >= CooldownThreshold && expiries.LatestAt + CooldownLength > now)
            throw new CooldownActiveException(
                product.Id, Math.Max(1, (int)Math.Ceiling((expiries.LatestAt!.Value + CooldownLength - now).TotalSeconds)));

        var open = await reservations.CountOpenAsync(customerId, product.Id, ct);
        if (open.ActiveHolds >= product.MaxActiveHolds || (onShortage is ShortagePolicy.Wait && open.Waiting >= product.MaxWaiting))
            throw new HoldLimitReachedException(product.Id, retryAfterSeconds: 30);
    }

    /// <inheritdoc cref="IReservationOperations.WaitAsync"/>
    public async Task<Reservation> WaitAsync(
        string customerId,
        Product product,
        string region,
        string? zone,
        string? clientReference,
        IReadOnlyList<ReservationLine> lines,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var reservation = Reservation.Wait(customerId, product.Id, region, zone, clientReference, lines, product.WaitMax, actor, now);
        await reservations.AddAsync(reservation, ct);
        return reservation;
    }

    /// <inheritdoc cref="IReservationOperations.PromoteAsync"/>
    public async Task PromoteAsync(
        Reservation reservation, HoldPlan plan, Product product, DateTimeOffset now, CancellationToken ct)
    {
        reservation.Promote(plan, product.HoldTtl, now, Actor.Sweeper);
        await reservations.UpdateAsync(reservation, ct);
    }

    /// <inheritdoc cref="IReservationOperations.TimeOutAsync"/>
    public async Task TimeOutAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        reservation.TimeOut(now, Actor.Sweeper);
        await reservations.UpdateAsync(reservation, ct);
    }

    /// <inheritdoc cref="IReservationOperations.LockWaitingAsync"/>
    public Task<IReadOnlyList<Reservation>> LockWaitingAsync(int limit, CancellationToken ct) =>
        reservations.LockWaitingAsync(limit, ct);

    /// <inheritdoc cref="IReservationOperations.LockTimedOutWaitingAsync"/>
    public Task<IReadOnlyList<Reservation>> LockTimedOutWaitingAsync(int limit, CancellationToken ct) =>
        reservations.LockTimedOutWaitingAsync(limit, ct);

    /// <inheritdoc cref="IReservationOperations.LockOpenAsync"/>
    public Task<IReadOnlyList<Reservation>> LockOpenAsync(string customerId, int limit, CancellationToken ct) =>
        reservations.LockOpenAsync(customerId, limit, ct);
}
