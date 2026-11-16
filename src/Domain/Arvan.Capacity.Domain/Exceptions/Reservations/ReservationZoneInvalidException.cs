namespace Arvan.Capacity.Domain.Exceptions.Reservations;

/// <summary>
/// Thrown when a request names a zone that does not belong to the requested region.
/// </summary>
/// <param name="zone">The zone named.</param>
/// <param name="region">The region requested.</param>
public sealed class ReservationZoneInvalidException(string zone, string region)
    : DomainValidationException(
        "reservation.zone_invalid",
        $"Zone '{zone}' does not belong to region '{region}'.");
