using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Reconciliation;

namespace Arvan.Capacity.Application.UseCases.Admin.ReconcileLedger;

/// <summary>
/// Computes and keeps the reconciliation report. Triggered hourly by the sweeper. Read-only with
/// respect to the counters: a drift is corrected by an operator with a ledger adjustment.
/// </summary>
public sealed class ReconcileLedgerUseCase(IReconciliationOperations reconciliation)
    : IUseCase<ReconcileLedgerInput, ReconcileLedgerOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public async Task<ReconcileLedgerOutput> ExecuteAsync(ReconcileLedgerInput input, CancellationToken ct) =>
        new((await reconciliation.ComputeAsync(ct)).DriftRows);
}
