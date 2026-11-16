namespace Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;

/// <summary>
/// The output of <see cref="PromoteWaitingUseCase"/>.
/// </summary>
/// <param name="Tried">How many waiting requests were tried.</param>
/// <param name="Promoted">How many of them got their capacity and are now held.</param>
public sealed record PromoteWaitingOutput(int Tried, int Promoted);
