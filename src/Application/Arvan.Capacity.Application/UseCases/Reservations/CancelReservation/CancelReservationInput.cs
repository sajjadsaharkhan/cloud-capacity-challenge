using Arvan.Capacity.Domain.Common;

namespace Arvan.Capacity.Application.UseCases.Reservations.CancelReservation;

/// <summary>
/// The input to <see cref="CancelReservationUseCase"/>.
/// </summary>
/// <param name="Id">The reservation.</param>
/// <param name="CustomerId">The owning customer.</param>
/// <param name="Actor">Who is calling.</param>
/// <param name="IdempotencyKey">The client's key; a retry with the same key returns the first result.</param>
public sealed record CancelReservationInput(Guid Id, string CustomerId, Actor Actor, string IdempotencyKey);
