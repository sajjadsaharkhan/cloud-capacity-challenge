using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Pools;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutPool;

/// <summary>
/// Creates or updates a pool. Corresponds to <c>PUT /v1/admin/pools/{id}</c>.
/// </summary>
public sealed class PutPoolUseCase(
    ITransactionManager transactionManager, IIdempotencyOperations idempotency, IPoolOperations pools)
    : IUseCase<PutPoolInput, PutPoolOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public Task<PutPoolOutput> ExecuteAsync(PutPoolInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            if (input.Actor.Kind is not ActorKind.Admin)
                throw new ActorNotAllowedException("change pools");

            var claim = await idempotency.ClaimAsync<PutPoolOutput>(
                input.Actor.Id, input.IdempotencyKey, $"admin.put_pool:{input.Id}", input with { IdempotencyKey = "" }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            var pool = await pools.PutAsync(input.Id, input.Region, input.Zone, input.HostRef, token);

            var output = new PutPoolOutput(pool.Id, pool.Region, pool.Zone, pool.HostRef, pool.AdminState, Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
