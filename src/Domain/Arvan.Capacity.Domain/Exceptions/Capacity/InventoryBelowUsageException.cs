using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Exceptions.Capacity;

/// <summary>
/// Thrown when an inventory change would leave less to sell than is already held and used.
/// </summary>
public sealed class InventoryBelowUsageException(int poolId, ResourceClass resourceClass, long held, long used)
    : DomainException(
        "inventory.below_usage",
        $"The new capacity of {resourceClass} in pool {poolId} is below what is already held ({held}) and allocated ({used}).");
