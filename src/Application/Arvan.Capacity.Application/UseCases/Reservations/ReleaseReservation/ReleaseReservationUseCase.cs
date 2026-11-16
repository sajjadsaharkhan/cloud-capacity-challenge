using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Mappers.Reservations;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.ReleaseReservation;

/// <summary>
/// Gives confirmed capacity back, when the service it backed was deleted or never came up.
/// Corresponds to <c>POST /v1/reservations/{id}/release</c>.
/// </summary>
public sealed class ReleaseReservationUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IIdempotencyOperations idempotency,
    IReservationOperations reservations,
    ICapacityOperations capacity)
    : IUseCase<ReleaseReservationInput, ReleaseReservationOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is a customer.</exception>
    public Task<ReleaseReservationOutput> ExecuteAsync(ReleaseReservationInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            // ── validate ──
            if (input.Actor.Kind is not (ActorKind.Service or ActorKind.Admin))
                throw new ActorNotAllowedException("release allocated capacity");

            var claim = await idempotency.ClaimAsync<ReleaseReservationOutput>(
                input.CustomerId, input.IdempotencyKey, $"release:{input.Id}", new { input.Id, input.Reason }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            // ── mutate ──
            var now = await clock.GetNowAsync(token);
            var reservation = await reservations.GetForUpdateAsync(input.Id, input.CustomerId, token);

            if (await reservations.ReleaseAsync(reservation, now, input.Actor, token))
                await capacity.ReleaseAsync(reservation, now, token);

            // ── shape ──
            var output = new ReleaseReservationOutput(reservation.ToSummary(now), Replayed: false);
            await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, reservation.Id, output, token);
            return output;
        }, ct);
}
