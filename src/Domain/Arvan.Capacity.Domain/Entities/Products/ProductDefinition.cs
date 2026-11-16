using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Products;

namespace Arvan.Capacity.Domain.Entities.Products;

/// <summary>
/// What an operator sets on a product: its status, allowed classes and reservation policy. Changes
/// apply to new reservations only.
/// </summary>
/// <param name="Status">Whether the product can be reserved.</param>
/// <param name="ResourceClasses">The classes a request may carry.</param>
/// <param name="HoldTtlSeconds">The lease length of a hold, 120 to 3600.</param>
/// <param name="WaitMaxSeconds">The longest a request may wait, up to 900.</param>
/// <param name="MaxActiveHolds">Open holds one customer may have on the product.</param>
/// <param name="MaxWaiting">Waiting requests one customer may have on the product.</param>
/// <param name="MaxPoolSharePct">The share of a pool one customer may hold, 1 to 100; 100 turns the cap off.</param>
public sealed record ProductDefinition(
    ProductStatus Status,
    IReadOnlyList<ResourceClass> ResourceClasses,
    int HoldTtlSeconds,
    int WaitMaxSeconds,
    int MaxActiveHolds,
    int MaxWaiting,
    int MaxPoolSharePct)
{
    /// <summary>
    /// Refuses a definition outside the policy ranges.
    /// </summary>
    /// <exception cref="ProductDefinitionInvalidException">Thrown when a value is out of range.</exception>
    public ProductDefinition EnsureValid() =>
        ResourceClasses.Count > 0
        && HoldTtlSeconds is >= 120 and <= 3600
        && WaitMaxSeconds is >= 0 and <= 900
        && MaxActiveHolds > 0
        && MaxWaiting >= 0
        && MaxPoolSharePct is >= 1 and <= 100
            ? this
            : throw new ProductDefinitionInvalidException();
}
