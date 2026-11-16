namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Where a resource class's capacity is counted.
/// </summary>
public enum ResourceScope
{
    /// <summary>
    /// Counted per zone: one pool per zone.
    /// </summary>
    Zone = 1,

    /// <summary>
    /// Counted per region: one pool for the whole region.
    /// </summary>
    Region = 2,

    /// <summary>
    /// Counted per host: one pool per host, and a request is served from a single host.
    /// </summary>
    Host = 3,
}
