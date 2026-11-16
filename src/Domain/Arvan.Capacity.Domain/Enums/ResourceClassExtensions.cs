namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Facts about <see cref="ResourceClass"/> that are configuration of the model rather than data.
/// </summary>
public static class ResourceClassExtensions
{
    /// <summary>
    /// Where this class's capacity is counted: public IPv4 addresses per region, GPUs per host,
    /// everything else per zone.
    /// </summary>
    /// <param name="resourceClass">The class.</param>
    public static ResourceScope ScopeOf(this ResourceClass resourceClass) =>
        resourceClass switch
        {
            ResourceClass.Ipv4 => ResourceScope.Region,
            ResourceClass.Gpu => ResourceScope.Host,
            _ => ResourceScope.Zone,
        };
}
