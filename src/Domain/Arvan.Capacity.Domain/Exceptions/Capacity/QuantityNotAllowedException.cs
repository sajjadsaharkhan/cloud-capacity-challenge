using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Exceptions.Capacity;

/// <summary>
/// Thrown when a requested quantity breaks a class's minimum, step size or maximum.
/// </summary>
public sealed class QuantityNotAllowedException(ResourceClass resourceClass, long quantity)
    : DomainValidationException(
        "capacity.quantity_not_allowed",
        $"Quantity {quantity} is not allowed for {resourceClass}: it must satisfy the minimum, the step size and the maximum.");
