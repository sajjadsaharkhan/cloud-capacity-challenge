using Arvan.Capacity.Domain.Entities.Products;

namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The catalog port for <see cref="Product"/>, read on the write connection because a decision in
/// the open transaction is about to depend on it. Reads are cached for a couple of seconds.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Finds a product by its code, or returns <c>null</c> if there is none.
    /// </summary>
    /// <param name="code">The product code.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Product?> FindAsync(string code, CancellationToken ct);

    /// <summary>
    /// Creates or updates a product and its policy.
    /// </summary>
    /// <param name="code">The product code.</param>
    /// <param name="definition">The definition to store.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task UpsertAsync(string code, ProductDefinition definition, CancellationToken ct);
}
