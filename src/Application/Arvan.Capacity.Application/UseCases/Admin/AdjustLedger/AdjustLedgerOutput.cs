namespace Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;

/// <summary>
/// The output of <see cref="AdjustLedgerUseCase"/>.
/// </summary>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record AdjustLedgerOutput(bool Replayed);
