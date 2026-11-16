using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// Settings under <c>Capacity</c> in configuration.
/// </summary>
public sealed class CapacityOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Capacity";

    /// <summary>
    /// The region this deployment serves, which prefixes every event id.
    /// </summary>
    public string Cell { get; init; } = "ir-thr";

    /// <summary>
    /// Per resource class, the quota a customer without a contract row gets. A missing row never
    /// means unlimited; <c>0</c> means a contract is required.
    /// </summary>
    public Dictionary<string, ResourceClassOptions> ResourceClasses { get; init; } = new()
    {
        ["VCPU"] = new() { DefaultQuota = 64000 },
        ["RAM_MB"] = new() { DefaultQuota = 131072 },
        ["DISK_GB"] = new() { DefaultQuota = 2000 },
        ["IPV4"] = new() { DefaultQuota = 10 },
        ["GPU"] = new() { DefaultQuota = 0 },
    };

    /// <summary>
    /// How long a product read is cached, in seconds.
    /// </summary>
    public int CatalogCacheSeconds { get; init; } = 2;

    /// <summary>
    /// The default quota of a class.
    /// </summary>
    /// <param name="resourceClass">The class.</param>
    public long DefaultQuotaOf(ResourceClass resourceClass) =>
        ResourceClasses.TryGetValue(DbEnum.ToDb(resourceClass), out var options) ? options.DefaultQuota : 0;
}

/// <summary>
/// Settings of one resource class.
/// </summary>
public sealed class ResourceClassOptions
{
    /// <summary>
    /// The quota a customer gets for the class when its contract has no row.
    /// </summary>
    public long DefaultQuota { get; init; }
}
