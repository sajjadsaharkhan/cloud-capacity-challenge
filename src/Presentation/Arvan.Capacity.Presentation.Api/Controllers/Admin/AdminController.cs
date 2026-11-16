using Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;
using Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;
using Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;
using Arvan.Capacity.Application.UseCases.Admin.GetReconciliation;
using Arvan.Capacity.Application.UseCases.Admin.PutInventory;
using Arvan.Capacity.Application.UseCases.Admin.PutPool;
using Arvan.Capacity.Application.UseCases.Admin.PutProduct;
using Arvan.Capacity.Application.UseCases.Admin.PutQuota;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Http;
using Arvan.Capacity.Mappers.Admin;
using Arvan.Capacity.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Arvan.Capacity.Controllers.Admin;

/// <summary>
/// Operator endpoints for the capacity catalog: pools, inventories, products, quotas, ledger
/// corrections and the reconciliation report.
/// </summary>
/// <remarks>
/// All require <c>X-Actor-Kind: ADMIN</c> (a service may take pools offline and bring them back, as
/// a health checker) and an <c>Idempotency-Key</c> on writes. Every change is logged with the actor.
/// </remarks>
[ApiController]
[Route("v1/admin")]
public sealed class AdminController(
    PutPoolUseCase putPool,
    ChangePoolStateUseCase changePoolState,
    PutInventoryUseCase putInventory,
    PutProductUseCase putProduct,
    PutQuotaUseCase putQuota,
    AdjustLedgerUseCase adjustLedger,
    CancelOpenReservationsUseCase cancelOpen,
    GetReconciliationUseCase getReconciliation,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Creates or updates a pool: its region, zone and host. A new pool is registered, not sold.
    /// </summary>
    [HttpPut("pools/{id:int}")]
    [ProducesResponseType(typeof(PoolResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PoolResponse>> PutPool(int id, PutPoolRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await putPool.ExecuteAsync(request.ToInput(id, actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogInformation("Admin {Actor} put pool {PoolId}: {Output}", actor.Id, id, output);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Moves a pool through its state machine: <c>activate</c>, <c>take_offline</c>, <c>restore</c>
    /// or <c>retire</c>.
    /// </summary>
    /// <remarks>
    /// Any combination the state machine does not allow is a 409. Taking a pool offline touches no
    /// held or allocated units: they finish normally.
    /// </remarks>
    [HttpPost("pools/{id:int}/state")]
    [ProducesResponseType(typeof(PoolStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PoolStateResponse>> ChangePoolState(int id, ChangePoolStateRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await changePoolState.ExecuteAsync(request.ToInput(id, actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogInformation("Admin {Actor} applied {Event} to pool {PoolId}: {State}", actor.Id, request.Event, id, output.AdminState);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Sets the size, margin, overcommit and quantity rules of one inventory of a pool.
    /// </summary>
    /// <remarks>
    /// A change that would leave less to sell than is already held and used is a 409
    /// <c>inventory.below_usage</c>. With <c>force</c> it is not applied; the pool is taken offline
    /// instead so no new holds land on it, and the response reports <c>held</c> and <c>used</c>.
    /// </remarks>
    [HttpPut("pools/{poolId:int}/inventories/{resourceClass}")]
    [ProducesResponseType(typeof(InventoryChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryChangeResponse>> PutInventory(
        int poolId, ResourceClass resourceClass, PutInventoryRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await putInventory.ExecuteAsync(
            request.ToInput(poolId, resourceClass, actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogInformation("Admin {Actor} put inventory {PoolId}/{Class}: {Request} -> {Output}", actor.Id, poolId, resourceClass, request, output);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Creates or updates a product and its policy.
    /// </summary>
    /// <remarks>
    /// Retiring a product blocks new submits but leaves existing holds to be confirmed, cancelled or
    /// expire. Policy changes apply to new reservations only.
    /// </remarks>
    [HttpPut("products/{code}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> PutProduct(string code, PutProductRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await putProduct.ExecuteAsync(request.ToInput(code, actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogInformation("Admin {Actor} put product {Code}: {Request}", actor.Id, code, request);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Sets a customer's contract limit for a class in a region.
    /// </summary>
    /// <remarks>
    /// Lowering it below current usage is allowed: existing holds and allocations stay, and new holds
    /// fail until usage drops under the limit.
    /// </remarks>
    [HttpPut("quotas/{customerId}/{resourceClass}/{region}")]
    [ProducesResponseType(typeof(QuotaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuotaResponse>> PutQuota(
        string customerId, ResourceClass resourceClass, string region, PutQuotaRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await putQuota.ExecuteAsync(
            request.ToInput(customerId, resourceClass, region, actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogInformation("Admin {Actor} put quota {Customer}/{Class}/{Region}: {Limit}", actor.Id, customerId, resourceClass, region, request.Limit);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Cancels a suspended customer's open holds and waiting requests, 200 per transaction.
    /// </summary>
    /// <remarks>
    /// Confirmed allocations are untouched: their fate belongs to billing.
    /// </remarks>
    [HttpPost("customers/{customerId}/cancel-open")]
    [ProducesResponseType(typeof(CancelOpenResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CancelOpenResponse>> CancelOpen(string customerId, CancellationToken ct)
    {
        _ = CallerHeaders.ReadIdempotencyKey(Request);
        var actor = CallerHeaders.ReadActor(Request);
        var output = await cancelOpen.ExecuteAsync(new CancelOpenReservationsInput(actor, customerId), ct);
        logger.LogInformation("Admin {Actor} cancelled {Count} open reservations of {Customer}", actor.Id, output.Cancelled, customerId);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Corrects a counter with one <c>ADJUST</c> ledger row. A reason is required.
    /// </summary>
    /// <remarks>
    /// The only way to change a counter outside a reservation transition. The change is applied
    /// under the counter's own constraints; one that would break them is a 422.
    /// </remarks>
    [HttpPost("ledger/adjustments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AdjustLedger(AdjustLedgerRequest request, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        await adjustLedger.ExecuteAsync(request.ToInput(actor, CallerHeaders.ReadIdempotencyKey(Request)), ct);
        logger.LogWarning("Admin {Actor} adjusted the ledger: {Request}", actor.Id, request);
        return Ok();
    }

    /// <summary>
    /// Retrieves the latest reconciliation report: counters against the ledger and the
    /// reservations, the unsettled backlog and the outbox backlog.
    /// </summary>
    /// <remarks>
    /// Computed hourly by the sweeper; pass <c>refresh=true</c> to compute it now. <c>driftRows</c>
    /// must be 0. Corrections are made with ledger adjustments, never automatically.
    /// </remarks>
    [HttpGet("reconciliation")]
    [ProducesResponseType(typeof(ReconciliationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationResponse>> GetReconciliation([FromQuery] bool refresh, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var report = await getReconciliation.ExecuteAsync(new GetReconciliationInput(actor.Kind, refresh), ct);
        return Ok(report.ToResponse());
    }
}
