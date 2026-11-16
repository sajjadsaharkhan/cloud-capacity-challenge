using Arvan.Capacity.Domain.Common;

namespace Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;

/// <summary>
/// The input to <see cref="CancelOpenReservationsUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="CustomerId">The suspended customer whose open reservations are cancelled.</param>
public sealed record CancelOpenReservationsInput(Actor Actor, string CustomerId);
