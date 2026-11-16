using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;
using Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;
using Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;
using Arvan.Capacity.Application.UseCases.Admin.PutInventory;
using Arvan.Capacity.Application.UseCases.Admin.PutPool;
using Arvan.Capacity.Application.UseCases.Admin.PutProduct;
using Arvan.Capacity.Application.UseCases.Admin.PutQuota;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.ViewModels.Admin;

namespace Arvan.Capacity.Mappers.Admin;

/// <summary>
/// Manual mappings between the admin endpoints' wire shapes and the Application layer's shapes.
/// The actor and the idempotency key come from headers, so they join the body here.
/// </summary>
public static class AdminMappers
{
    /// <summary>
    /// Maps a pool body, the route id and headers to <see cref="PutPoolInput"/>.
    /// </summary>
    public static PutPoolInput ToInput(this PutPoolRequest request, int id, Actor actor, string key) =>
        new(actor, key, id, request.Region, request.Zone, request.HostRef);

    /// <summary>
    /// Maps a state-change body, the route id and headers to <see cref="ChangePoolStateInput"/>.
    /// </summary>
    public static ChangePoolStateInput ToInput(this ChangePoolStateRequest request, int id, Actor actor, string key) =>
        new(actor, key, id, request.Event);

    /// <summary>
    /// Maps an inventory body, the route and headers to <see cref="PutInventoryInput"/>.
    /// </summary>
    public static PutInventoryInput ToInput(this PutInventoryRequest request, int poolId, ResourceClass resourceClass, Actor actor, string key) =>
        new(actor, key, poolId, resourceClass,
            new InventoryConfiguration(request.Total, request.Reserved, request.AllocationRatio, request.MinUnit, request.StepSize, request.MaxUnit),
            request.Force);

    /// <summary>
    /// Maps a product body, the route code and headers to <see cref="PutProductInput"/>.
    /// </summary>
    public static PutProductInput ToInput(this PutProductRequest request, string code, Actor actor, string key) =>
        new(actor, key, code,
            new ProductDefinition(request.Status, request.ResourceClasses, request.HoldTtlSeconds, request.WaitMaxSeconds,
                request.MaxActiveHolds, request.MaxWaiting, request.MaxPoolSharePct));

    /// <summary>
    /// Maps a quota body, the route and headers to <see cref="PutQuotaInput"/>.
    /// </summary>
    public static PutQuotaInput ToInput(this PutQuotaRequest request, string customerId, ResourceClass resourceClass, string region, Actor actor, string key) =>
        new(actor, key, customerId, resourceClass, region, request.Limit);

    /// <summary>
    /// Maps an adjustment body and headers to <see cref="AdjustLedgerInput"/>.
    /// </summary>
    public static AdjustLedgerInput ToInput(this AdjustLedgerRequest request, Actor actor, string key) =>
        new(actor, key, request.PoolId, request.CustomerId, request.Region, request.ResourceClass,
            request.DeltaHeld, request.DeltaUsed, request.Reason);

    /// <summary>
    /// Maps a pool result to <see cref="PoolResponse"/>.
    /// </summary>
    public static PoolResponse ToResponse(this PutPoolOutput o) => new(o.Id, o.Region, o.Zone, o.HostRef, o.AdminState);

    /// <summary>
    /// Maps a state-change result to <see cref="PoolStateResponse"/>.
    /// </summary>
    public static PoolStateResponse ToResponse(this ChangePoolStateOutput o) => new(o.Id, o.AdminState);

    /// <summary>
    /// Maps an inventory result to <see cref="InventoryChangeResponse"/>.
    /// </summary>
    public static InventoryChangeResponse ToResponse(this PutInventoryOutput o) => new(o.Applied, o.PoolTakenOffline, o.Held, o.Used);

    /// <summary>
    /// Maps a product result to <see cref="ProductResponse"/>.
    /// </summary>
    public static ProductResponse ToResponse(this PutProductOutput o) =>
        new(o.Code, o.Definition.Status, o.Definition.ResourceClasses, o.Definition.HoldTtlSeconds, o.Definition.WaitMaxSeconds,
            o.Definition.MaxActiveHolds, o.Definition.MaxWaiting, o.Definition.MaxPoolSharePct);

    /// <summary>
    /// Maps a quota result to <see cref="QuotaResponse"/>.
    /// </summary>
    public static QuotaResponse ToResponse(this PutQuotaOutput o) => new(o.CustomerId, o.ResourceClass, o.Region, o.Limit);

    /// <summary>
    /// Maps a cancel-open result to <see cref="CancelOpenResponse"/>.
    /// </summary>
    public static CancelOpenResponse ToResponse(this CancelOpenReservationsOutput o) => new(o.Cancelled);

    /// <summary>
    /// Maps a reconciliation report to <see cref="ReconciliationResponse"/>.
    /// </summary>
    public static ReconciliationResponse ToResponse(this ReconciliationReport r) =>
        new(r.GeneratedAt, r.DriftRows, [.. r.Drift.Select(d => new DriftRowResponse(d.Check, d.Key, d.Counter, d.Expected, d.Actual))],
            r.UnsettledHolds, r.UnsettledWaiting, r.OutboxBacklog, r.OutboxOldestAgeSeconds);
}
