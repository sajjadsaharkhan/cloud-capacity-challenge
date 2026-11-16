namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when a confirm or an extend reaches a hold whose lease has ended.
/// </summary>
public sealed class ReservationExpiredException(Guid reservationId)
    : DomainException(
        "reservation.expired",
        $"Reservation {reservationId} is past its deadline and can no longer be confirmed or extended.");
