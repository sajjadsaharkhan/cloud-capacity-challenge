using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Entities.Products;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The read-side port for capacity: levels, the advisory fit-check, and a customer's quota room.
/// </summary>
public interface ICapacityReadonlyRepository : IReadonlyRepository<Pool>
{
    /// <summary>
    /// Reads the inventories of available pools, optionally of one region, with expired-unsettled
    /// holds counted back as free.
    /// </summary>
    /// <param name="region">When set, only this region.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<CapacityInventoryRow>> GetInventoryRowsAsync(string? region, CancellationToken ct);

    /// <summary>
    /// Reads the products, optionally one.
    /// </summary>
    /// <param name="productCode">When set, only this product.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Product>> GetProductsAsync(string? productCode, CancellationToken ct);

    /// <summary>
    /// Reads, without locks, the inventories of every pool in a region for the given classes.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="classes">The classes wanted.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<InventorySnapshot>> GetSnapshotsAsync(string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct);

    /// <summary>
    /// How much of its contract a customer still has room for, per class in a region: limit minus
    /// held and used. A customer with no row has the class's default quota.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="region">The region.</param>
    /// <param name="classes">The classes wanted.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyDictionary<ResourceClass, long>> GetQuotaRoomAsync(
        string customerId, string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct);
}
