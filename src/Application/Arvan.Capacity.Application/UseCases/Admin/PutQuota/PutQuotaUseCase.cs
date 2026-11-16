using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Quotas;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutQuota;

/// <summary>
/// Sets a customer's contract limit. Lowering it below current usage is allowed: existing holds and
/// allocations stay, and new holds fail until usage drops under the limit. Corresponds to
/// <c>PUT /v1/admin/quotas/{customer}/{class}/{region}</c>.
/// </summary>
public sealed class PutQuotaUseCase(
    ITransactionManager transactionManager, IIdempotencyOperations idempotency, IQuotaOperations quotas)
    : IUseCase<PutQuotaInput, PutQuotaOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public Task<PutQuotaOutput> ExecuteAsync(PutQuotaInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            if (input.Actor.Kind is not ActorKind.Admin)
                throw new ActorNotAllowedException("change quotas");

            var claim = await idempotency.ClaimAsync<PutQuotaOutput>(
                input.Actor.Id, input.IdempotencyKey, $"admin.put_quota:{input.CustomerId}:{input.ResourceClass}:{input.Region}",
                new { input.Limit }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            await quotas.SetLimitAsync(input.CustomerId, input.ResourceClass, input.Region, input.Limit, token);

            var output = new PutQuotaOutput(input.CustomerId, input.ResourceClass, input.Region, input.Limit, Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
