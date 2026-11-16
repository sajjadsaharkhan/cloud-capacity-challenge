using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Framework.Domain.Abstractions;

namespace Arvan.Capacity.Domain.Entities.Products;

/// <summary>
/// A sellable product and its reservation policy, in one row: which resource classes a request
/// may carry and how long a hold lives. Adding a product is a data change.
/// </summary>
public sealed class Product : BaseEntity<string>
{
    /// <summary>
    /// Whether the product can be reserved.
    /// </summary>
    public ProductStatus Status { get; private set; }

    /// <summary>
    /// The resource classes a request for this product may carry.
    /// </summary>
    public IReadOnlyList<ResourceClass> ResourceClasses { get; private set; } = [];

    /// <summary>
    /// The lease length, in seconds, of a hold on this product.
    /// </summary>
    public int HoldTtlSeconds { get; private set; }

    /// <summary>
    /// How long a waiting request may wait for capacity, in seconds.
    /// </summary>
    public int WaitMaxSeconds { get; private set; }

    /// <summary>
    /// Open holds one customer may have on this product.
    /// </summary>
    public int MaxActiveHolds { get; private set; }

    /// <summary>
    /// Waiting requests one customer may have on this product.
    /// </summary>
    public int MaxWaiting { get; private set; }

    /// <summary>
    /// The share of a pool, in percent, one customer may hold; 100 means no cap.
    /// </summary>
    public int MaxPoolSharePct { get; private set; }

    /// <summary>
    /// Reserved for materialization by the persistence layer.
    /// </summary>
    private Product()
    {
    }

    /// <summary>
    /// The lease length of a hold on this product.
    /// </summary>
    public TimeSpan HoldTtl => TimeSpan.FromSeconds(HoldTtlSeconds);

    /// <summary>
    /// How long a waiting request may wait for capacity.
    /// </summary>
    public TimeSpan WaitMax => TimeSpan.FromSeconds(WaitMaxSeconds);

    /// <summary>
    /// Whether a request for this product may be taken at all: the product is active and allows
    /// every one of <paramref name="classes"/>.
    /// </summary>
    /// <param name="classes">The resource classes the request carries.</param>
    public bool Accepts(IEnumerable<ResourceClass> classes) =>
        Status is ProductStatus.Active && classes.All(ResourceClasses.Contains);
}
