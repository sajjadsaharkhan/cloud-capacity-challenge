namespace Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;

/// <summary>
/// The output of <see cref="ExpireHoldsUseCase"/>.
/// </summary>
/// <param name="Expired">The number of holds finalized. A full batch means more may be waiting.</param>
public sealed record ExpireHoldsOutput(int Expired);
