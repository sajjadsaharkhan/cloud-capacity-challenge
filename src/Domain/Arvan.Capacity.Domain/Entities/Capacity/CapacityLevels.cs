using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// Turns effective free capacity into the level a customer sees.
/// </summary>
public static class CapacityLevels
{
    /// <summary>
    /// The level of one resource class: unavailable when nothing is free, limited when free capacity
    /// is below 10% of what can be sold, otherwise available.
    /// </summary>
    /// <param name="effectiveFree">Free units, counting holds past their lease as free.</param>
    /// <param name="sellable">Units that can be sold across the pools counted.</param>
    public static CapacityLevel Of(long effectiveFree, long sellable) =>
        effectiveFree <= 0 ? CapacityLevel.Unavailable
        : effectiveFree * 10 < sellable ? CapacityLevel.Limited
        : CapacityLevel.Available;

    /// <summary>
    /// The level of a product: the worst level across its classes.
    /// </summary>
    /// <param name="levels">The level of each class.</param>
    public static CapacityLevel Worst(IEnumerable<CapacityLevel> levels) =>
        levels.DefaultIfEmpty(CapacityLevel.Unavailable).Min();
}
