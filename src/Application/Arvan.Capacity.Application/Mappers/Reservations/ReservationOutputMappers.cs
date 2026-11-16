using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Mappers.Reservations;

/// <summary>
/// Manual mappings from the <see cref="Reservation"/> entity to the projection every reservation
/// UseCase returns.
/// </summary>
public static class ReservationOutputMappers
{
    /// <summary>
    /// Shapes a reservation as a reader would see it at <paramref name="now"/>. Must agree with the
    /// <c>reservation_effective</c> view the read side goes through.
    /// </summary>
    /// <param name="reservation">The reservation.</param>
    /// <param name="now">The database time.</param>
    public static ReservationSummary ToSummary(this Reservation reservation, DateTimeOffset now)
    {
        var status = reservation.EffectiveStatusAt(now);
        var settled = status == reservation.StoredStatus;

        var reason = settled
            ? reservation.ReasonCode
            : status is ReservationStatus.Expired ? ReasonCode.HoldExpired : ReasonCode.WaitTimeout;

        return new ReservationSummary(
            reservation.Id,
            status,
            settled,
            reason,
            reservation.ProductCode,
            reservation.Region,
            reservation.Zone,
            [.. reservation.Lines.Select(l => new ReservationLineSummary(l.ResourceClass, l.Quantity))],
            reservation.ExpiresAt,
            reservation.WaitDeadline,
            reservation.Extended,
            reservation.AllocationState,
            reservation.ClientReference,
            reservation.Version,
            reservation.CreatedAt,
            reservation.UpdatedAt);
    }
}
