namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when a hold is extended a second time.
/// </summary>
public sealed class ReservationAlreadyExtendedException(Guid reservationId)
    : DomainException(
        "reservation.already_extended",
        $"Reservation {reservationId} has already been extended once.");
