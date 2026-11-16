using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Mappers.Reservations;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Reservations;

namespace Arvan.Capacity.Application.UseCases.Reservations.ExtendReservation;

/// <summary>
/// Extends a held reservation's lease once, by its product's TTL. Corresponds to
/// <c>POST /v1/reservations/{id}/extend</c>.
/// </summary>
public sealed class ExtendReservationUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IIdempotencyOperations idempotency,
    IReservationOperations reservations,
    ICatalogOperations catalog)
    : IUseCase<ExtendReservationInput, ExtendReservationOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<ExtendReservationOutput> ExecuteAsync(ExtendReservationInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            // ── validate ──
            var claim = await idempotency.ClaimAsync<ExtendReservationOutput>(
                input.CustomerId, input.IdempotencyKey, $"extend:{input.Id}", new { input.Id }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            // ── mutate ──
            var now = await clock.GetNowAsync(token);
            var reservation = await reservations.GetForUpdateAsync(input.Id, input.CustomerId, token);
            var product = await catalog.GetAsync(reservation.ProductCode, token);
            await reservations.ExtendAsync(reservation, now, product.HoldTtl, input.Actor, token);

            // ── shape ──
            var output = new ExtendReservationOutput(reservation.ToSummary(now), Replayed: false);
            await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, reservation.Id, output, token);
            return output;
        }, ct);
}
