using Arvan.Capacity.Application.Repositories.Readonly;

namespace Arvan.Capacity.Application.Operations.Reconciliation;

/// <summary>
/// Owns the reconciliation report. Read-only: a drift is corrected by a ledger adjustment, never
/// here. Never begins or commits a transaction.
/// </summary>
public interface IReconciliationOperations
{
    /// <summary>
    /// Computes the report now and keeps it as the latest.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ReconciliationReport> ComputeAsync(CancellationToken ct);

    /// <summary>
    /// The latest stored report, computed first if there is none.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ReconciliationReport> GetLatestAsync(CancellationToken ct);
}
