namespace Arvan.Capacity.Application.UseCases.Admin.ReconcileLedger;

/// <summary>
/// The output of <see cref="ReconcileLedgerUseCase"/>.
/// </summary>
/// <param name="DriftRows">The number of drifting rows; must be 0.</param>
public sealed record ReconcileLedgerOutput(int DriftRows);
