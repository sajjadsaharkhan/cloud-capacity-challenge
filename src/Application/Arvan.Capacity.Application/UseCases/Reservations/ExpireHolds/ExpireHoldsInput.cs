namespace Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;

/// <summary>
/// The input to <see cref="ExpireHoldsUseCase"/>.
/// </summary>
/// <param name="BatchSize">The largest number of holds to finalize in one transaction.</param>
public sealed record ExpireHoldsInput(int BatchSize);
