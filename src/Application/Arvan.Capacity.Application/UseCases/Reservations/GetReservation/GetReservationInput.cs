namespace Arvan.Capacity.Application.UseCases.Reservations.GetReservation;

/// <summary>
/// The input to <see cref="GetReservationUseCase"/>.
/// </summary>
/// <param name="Id">The reservation to retrieve.</param>
/// <param name="CustomerId">The owning customer; another customer's reservation reads as absent.</param>
public sealed record GetReservationInput(Guid Id, string CustomerId);
