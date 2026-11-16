using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Pools;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;

/// <summary>
/// Moves a pool through the resource state machine. Corresponds to
/// <c>POST /v1/admin/pools/{id}/state</c>.
/// </summary>
public sealed class ChangePoolStateUseCase(
    ITransactionManager transactionManager, IIdempotencyOperations idempotency, IPoolOperations pools)
    : IUseCase<ChangePoolStateInput, ChangePoolStateOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller may not make this change.</exception>
    public Task<ChangePoolStateOutput> ExecuteAsync(ChangePoolStateInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            // A health checker runs as a service and may only take pools offline and bring them back.
            var allowed = input.Actor.Kind is ActorKind.Admin
                || (input.Actor.Kind is ActorKind.Service && input.Event is "take_offline" or "restore");
            if (!allowed)
                throw new ActorNotAllowedException($"apply '{input.Event}' to a pool");

            var claim = await idempotency.ClaimAsync<ChangePoolStateOutput>(
                input.Actor.Id, input.IdempotencyKey, $"admin.pool_state:{input.Id}", new { input.Id, input.Event }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            var pool = await pools.ChangeStateAsync(input.Id, input.Event, token);

            var output = new ChangePoolStateOutput(pool.Id, pool.AdminState, Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
