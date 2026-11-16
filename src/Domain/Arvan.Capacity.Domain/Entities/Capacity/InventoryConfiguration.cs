using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// What an operator sets on an inventory: its size, margin, overcommit and quantity rules. Never
/// the counters, which only reservation transitions and ledger adjustments move.
/// </summary>
/// <param name="Total">The physical total.</param>
/// <param name="Reserved">The fragmentation margin kept out of sale.</param>
/// <param name="AllocationRatio">The overcommit ratio, at least 1.</param>
/// <param name="MinUnit">The smallest quantity one line may ask for.</param>
/// <param name="StepSize">The quantity a line must be a multiple of.</param>
/// <param name="MaxUnit">The largest quantity one line may ask for.</param>
public sealed record InventoryConfiguration(
    long Total,
    long Reserved,
    decimal AllocationRatio,
    long MinUnit,
    long StepSize,
    long MaxUnit)
{
    /// <summary>
    /// What can be sold under this configuration.
    /// </summary>
    public long Sellable => InventorySnapshot.ComputeSellable(Total, Reserved, AllocationRatio);

    /// <summary>
    /// Refuses a configuration the table's constraints would refuse.
    /// </summary>
    /// <exception cref="InventoryConfigurationInvalidException">Thrown when a value is out of range.</exception>
    public InventoryConfiguration EnsureValid() =>
        AllocationRatio >= 1 && Reserved >= 0 && Reserved <= Total && Total >= 0
        && MinUnit > 0 && StepSize > 0 && MaxUnit >= MinUnit
            ? this
            : throw new InventoryConfigurationInvalidException();

    /// <summary>
    /// Whether the configuration would leave less to sell than is already held and used.
    /// </summary>
    /// <param name="held">Units currently held.</param>
    /// <param name="used">Units currently allocated.</param>
    public bool IsBelowUsage(long held, long used) => Sellable < held + used;
}
