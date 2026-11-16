using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutProduct;

/// <summary>
/// Creates or updates a product and its policy. Retiring a product blocks new submits but leaves
/// existing holds to be confirmed, cancelled or expire. Corresponds to <c>PUT /v1/admin/products/{code}</c>.
/// </summary>
public sealed class PutProductUseCase(
    ITransactionManager transactionManager, IIdempotencyOperations idempotency, ICatalogOperations catalog)
    : IUseCase<PutProductInput, PutProductOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public Task<PutProductOutput> ExecuteAsync(PutProductInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            if (input.Actor.Kind is not ActorKind.Admin)
                throw new ActorNotAllowedException("change products");

            var claim = await idempotency.ClaimAsync<PutProductOutput>(
                input.Actor.Id, input.IdempotencyKey, $"admin.put_product:{input.Code}",
                new { input.Code, input.Definition }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            await catalog.UpsertAsync(input.Code, input.Definition, token);

            var output = new PutProductOutput(input.Code, input.Definition, Replayed: false);
            await idempotency.CompleteAsync(input.Actor.Id, input.IdempotencyKey, null, output, token);
            return output;
        }, ct);
}
