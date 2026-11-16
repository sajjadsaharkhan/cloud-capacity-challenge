namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a client reference already names a live reservation of the same customer. A retry
/// with a fresh idempotency key lands here instead of creating a duplicate.
/// </summary>
/// <param name="clientReference">The reference that is taken, when known: a race lost at the unique index does not name it.</param>
public sealed class ReservationClientReferenceTakenException(string? clientReference)
    : ConflictException(
        "reservation.client_reference_taken",
        clientReference is null
            ? "The client reference already names a waiting, held or confirmed reservation."
            : $"Client reference '{clientReference}' already names a waiting, held or confirmed reservation.");
