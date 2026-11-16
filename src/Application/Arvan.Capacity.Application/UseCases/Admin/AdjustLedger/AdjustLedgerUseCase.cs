using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;

/// <summary>
/// Corrects a counter with one <c>ADJUST</c> ledger row: the only way to change a counter outside a
/// reservation transition. Corresponds to <c>POST /v1/admin/ledger/adjustments</c>.
/// </summary>
public sealed class AdjustLedgerUseCase(
    ITransactionManager transactionManager, IClock clock, IIdempotencyOperations idempotency, ICapacityOperations capacity)
    : IUseCase<AdjustLedgerInput, AdjustLedgerOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    /// <exception cref="UnprocessableException">Thrown when the target is ambiguous or the reason is blank.</exception>
    public Task<AdjustLedgerOutput> ExecuteAsync(AdjustLedgerInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            if (input.Actor.Kind is not ActorKind.Admin)
                throw new ActorNotAllowedException("adjust the ledger");

            var forInventory = input.PoolId is not null;
            var forQuota = input.CustomerId is not null && input.Region is not null;
            if (forInventory == forQuota || string.IsNullOrWhiteSpace(input.Reason))
                throw new UnprocessableException(
                    "ledger.adjustment_invalid", "An adjustment names a pool or a customer and region, and needs a reason.");

            var claim = await idempotency.ClaimAsync<AdjustLedgerOutput>(
                input.Actor.Id, input.IdempotencyKey, "admin.adjust_ledger", input with { IdempotencyKey = "" }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            var now = await clock.GetNowAsync(token);
            await capacity.AdjustAsync(
                new LedgerAdjustment(input.PoolId, input.CustomerId, input.Region, input.ResourceClass,
                    input.DeltaHeld, input.DeltaUsed, input.Actor.Id, input.Reason),
                now, token);

            var output = new AdjustLedgerOutput(Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
