using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.ReleaseReservation;

/// <summary>
/// The input to <see cref="ReleaseReservationUseCase"/>.
/// </summary>
/// <param name="Id">The reservation.</param>
/// <param name="CustomerId">The owning customer.</param>
/// <param name="Actor">Who is calling; must be a service or an admin.</param>
/// <param name="IdempotencyKey">The client's key; a retry with the same key returns the first result.</param>
/// <param name="Reason">Why the capacity is being released.</param>
public sealed record ReleaseReservationInput(
    Guid Id, string CustomerId, Actor Actor, string IdempotencyKey, ReleaseReason Reason);
