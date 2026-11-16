namespace Arvan.Capacity.Application.UseCases.Reservations.PurgeIdempotencyKeys;

/// <summary>
/// The input to <see cref="PurgeIdempotencyKeysUseCase"/>.
/// </summary>
/// <param name="BatchSize">The largest number of keys to delete in one transaction.</param>
public sealed record PurgeIdempotencyKeysInput(int BatchSize);
