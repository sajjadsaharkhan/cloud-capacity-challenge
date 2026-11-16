namespace Arvan.Capacity.Application.UseCases.Admin.PutInventory;

/// <summary>
/// The output of <see cref="PutInventoryUseCase"/>.
/// </summary>
/// <param name="Applied">Whether the configuration was applied.</param>
/// <param name="PoolTakenOffline">Whether a forced change took the pool offline instead of being applied.</param>
/// <param name="Held">Units held at the time.</param>
/// <param name="Used">Units allocated at the time.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record PutInventoryOutput(bool Applied, bool PoolTakenOffline, long Held, long Used, bool Replayed);
