using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when an operation is not legal from the reservation's current effective status.
/// </summary>
public sealed class ReservationInvalidTransitionException(Guid reservationId, ReservationStatus status, string attempted)
    : DomainException(
        "reservation.invalid_state_transition",
        $"Reservation {reservationId} is {status} and cannot {attempted}.");
