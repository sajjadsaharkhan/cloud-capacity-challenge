using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Implementations.Catalog;

/// <inheritdoc cref="ICatalogOperations"/>
public sealed class CatalogOperations(IProductRepository products) : ICatalogOperations
{
    /// <inheritdoc cref="ICatalogOperations.GetReservableAsync"/>
    public async Task<Product> GetReservableAsync(string code, IEnumerable<ResourceClass> classes, CancellationToken ct)
    {
        var product = await products.FindAsync(code, ct);

        return product is not null && product.Accepts(classes)
            ? product
            : throw new ProductNotAvailableException(code);
    }

    /// <inheritdoc cref="ICatalogOperations.GetAsync"/>
    public async Task<Product> GetAsync(string code, CancellationToken ct) =>
        await products.FindAsync(code, ct)
        ?? throw new EntityNotFoundException<Product>(code);

    /// <inheritdoc cref="ICatalogOperations.UpsertAsync"/>
    public Task UpsertAsync(string code, ProductDefinition definition, CancellationToken ct) =>
        products.UpsertAsync(code, definition.EnsureValid(), ct);
}
