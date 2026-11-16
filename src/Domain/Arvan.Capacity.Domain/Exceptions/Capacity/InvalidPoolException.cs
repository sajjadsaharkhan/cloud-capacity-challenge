namespace Arvan.Capacity.Domain.Exceptions.Capacity;

/// <summary>
/// Thrown when a pool is described without a region, or a host without a zone.
/// </summary>
public sealed class InvalidPoolException()
    : DomainValidationException(
        "pool.invalid",
        "A pool needs a region, and a host needs a zone.");
