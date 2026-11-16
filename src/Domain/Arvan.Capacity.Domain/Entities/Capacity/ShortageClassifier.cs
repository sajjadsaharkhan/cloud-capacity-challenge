using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// Explains why a line could not be held: not enough capacity ever, none left, or none for now.
/// A pure function of a snapshot; a snapshot a few milliseconds old changes the advice, never the
/// decision.
/// </summary>
public static class ShortageClassifier
{
    /// <summary>
    /// Classifies one short line against every pool that could serve its class.
    /// </summary>
    /// <param name="quantity">The quantity the line asked for.</param>
    /// <param name="pools">The inventories of the class in scope. Pools that are not available or offline are ignored.</param>
    /// <returns>
    /// <see cref="ReasonCode.ExceedsRegisteredCapacity"/> when no pool could ever provide it,
    /// <see cref="ReasonCode.CapacitySoldOut"/> when live allocations fill what could, otherwise
    /// <see cref="ReasonCode.CapacityTemporarilyUnavailable"/>.
    /// </returns>
    public static ReasonCode Classify(long quantity, IReadOnlyCollection<InventorySnapshot> pools)
    {
        var registered = pools
            .Where(p => p.AdminState is PoolAdminState.Available or PoolAdminState.Offline)
            .ToList();

        var maxSellable = registered.Select(p => p.Sellable).DefaultIfEmpty(0).Max();
        if (quantity > maxSellable)
            return ReasonCode.ExceedsRegisteredCapacity;

        var maxWithoutHolds = registered.Select(p => p.Sellable - p.Used).DefaultIfEmpty(0).Max();
        return quantity > maxWithoutHolds
            ? ReasonCode.CapacitySoldOut
            : ReasonCode.CapacityTemporarilyUnavailable;
    }

    /// <summary>
    /// The code for a whole reservation: the most severe of its short lines.
    /// </summary>
    /// <param name="lineCodes">The code of each short line.</param>
    public static ReasonCode MostSevere(IEnumerable<ReasonCode> lineCodes) => lineCodes.Max();
}
