namespace Arvan.Capacity.Domain.Exceptions.Capacity;

/// <summary>
/// Thrown when an inventory configuration breaks the table's constraints.
/// </summary>
public sealed class InventoryConfigurationInvalidException()
    : DomainValidationException(
        "inventory.configuration_invalid",
        "An inventory needs total >= reserved >= 0, an allocation ratio of at least 1, and 0 < min <= max with a positive step.");
