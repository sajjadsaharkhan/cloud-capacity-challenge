namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The outcome of trying to take a reservation's lines out of free capacity.
/// </summary>
public enum HoldResult
{
    /// <summary>
    /// Every line was taken from quota and inventory.
    /// </summary>
    Held = 1,

    /// <summary>
    /// A line would have exceeded the customer's contract limit.
    /// </summary>
    QuotaExceeded = 2,

    /// <summary>
    /// A line would have exceeded the customer's share cap of a scarce pool.
    /// </summary>
    PoolShareExceeded = 3,

    /// <summary>
    /// A line did not fit the pool's sellable capacity.
    /// </summary>
    CapacityShort = 4,
}
