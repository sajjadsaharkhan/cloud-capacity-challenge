namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a product is unknown, retired, or does not allow a requested resource class.
/// </summary>
/// <param name="productCode">The product code requested.</param>
public sealed class ProductNotAvailableException(string productCode)
    : UnprocessableException(
        "product.not_available",
        $"Product '{productCode}' is unknown, retired, or does not allow one of the requested resource classes.");
