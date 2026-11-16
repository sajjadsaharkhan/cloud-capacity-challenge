using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Catalog;

/// <summary>
/// Owns validation and orchestration for <see cref="Product"/>. Never begins or commits a
/// transaction, never calls a peer Operation.
/// </summary>
public interface ICatalogOperations
{
    /// <summary>
    /// Retrieves the product a request targets and checks that it can take the request.
    /// </summary>
    /// <param name="code">The product code requested.</param>
    /// <param name="classes">The resource classes the request carries.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="ProductNotAvailableException">Thrown when the product is unknown, retired, or does not allow a requested class.</exception>
    Task<Product> GetReservableAsync(string code, IEnumerable<ResourceClass> classes, CancellationToken ct);

    /// <summary>
    /// Retrieves a product's policy whatever its status, for a reservation that already exists.
    /// </summary>
    /// <param name="code">The product code.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="NotFoundException">Thrown when no such product exists.</exception>
    Task<Product> GetAsync(string code, CancellationToken ct);

    /// <summary>
    /// Creates or updates a product and its policy. Changes apply to new reservations only.
    /// </summary>
    /// <param name="code">The product code.</param>
    /// <param name="definition">The definition to store.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="Domain.Exceptions.DomainValidationException">Thrown when the definition is outside the policy ranges.</exception>
    Task UpsertAsync(string code, ProductDefinition definition, CancellationToken ct);
}
