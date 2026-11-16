using Arvan.Capacity.Application.Operations.Reconciliation;
using Arvan.Capacity.Application.Repositories.Readonly;

namespace Arvan.Capacity.Application.Operations.Implementations.Reconciliation;

/// <inheritdoc cref="IReconciliationOperations"/>
public sealed class ReconciliationOperations(IReconciliationRepository reports) : IReconciliationOperations
{
    /// <inheritdoc cref="IReconciliationOperations.ComputeAsync"/>
    public async Task<ReconciliationReport> ComputeAsync(CancellationToken ct)
    {
        var report = await reports.ComputeAsync(ct);
        await reports.SaveAsync(report, ct);
        return report;
    }

    /// <inheritdoc cref="IReconciliationOperations.GetLatestAsync"/>
    public async Task<ReconciliationReport> GetLatestAsync(CancellationToken ct) =>
        await reports.GetLatestAsync(ct) ?? await ComputeAsync(ct);
}
