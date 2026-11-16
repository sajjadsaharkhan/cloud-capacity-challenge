namespace Arvan.Capacity.Application.UseCases.Reservations.PurgeIdempotencyKeys;

/// <summary>
/// The output of <see cref="PurgeIdempotencyKeysUseCase"/>.
/// </summary>
/// <param name="Purged">The number of keys deleted. A full batch means more may be waiting.</param>
public sealed record PurgeIdempotencyKeysOutput(int Purged);
