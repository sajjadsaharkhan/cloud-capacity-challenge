using Arvan.Capacity.Domain.Constants;

namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when a reservation has no lines, too many, a repeated class, or a non-positive quantity.
/// </summary>
public sealed class ReservationLinesInvalidException()
    : DomainValidationException(
        "reservation.lines_invalid",
        $"A reservation needs 1 to {ReservationConstant.MaxLines} lines, each with a distinct resource class and a positive quantity.");
