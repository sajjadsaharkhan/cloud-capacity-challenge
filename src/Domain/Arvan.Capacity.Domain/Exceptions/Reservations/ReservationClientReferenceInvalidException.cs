using Arvan.Capacity.Domain.Constants;

namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when a client reference is too long.
/// </summary>
public sealed class ReservationClientReferenceInvalidException()
    : DomainValidationException(
        "reservation.client_reference_invalid",
        $"Client reference must be at most {ReservationConstant.ClientReferenceMaxLength} characters.");
