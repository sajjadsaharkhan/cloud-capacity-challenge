namespace Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;

/// <summary>
/// The input to <see cref="PromoteWaitingUseCase"/>.
/// </summary>
/// <param name="BatchSize">The largest number of waiting requests to try in one transaction.</param>
public sealed record PromoteWaitingInput(int BatchSize);
