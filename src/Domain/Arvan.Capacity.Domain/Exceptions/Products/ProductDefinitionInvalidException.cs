namespace Arvan.Capacity.Domain.Exceptions.Products;

/// <summary>
/// Thrown when a product definition is outside the policy ranges.
/// </summary>
public sealed class ProductDefinitionInvalidException()
    : DomainValidationException(
        "product.definition_invalid",
        "A product needs at least one class, a hold TTL of 120-3600 s, a wait of at most 900 s, positive limits and a pool share of 1-100.");
