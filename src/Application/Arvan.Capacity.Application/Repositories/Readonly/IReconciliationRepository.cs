using Arvan.Capacity.Domain.Entities.Capacity;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The read-side port for the reconciliation report, and for keeping the latest one.
/// </summary>
public interface IReconciliationRepository : IReadonlyRepository<Pool>
{
    /// <summary>
    /// Computes the report now.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ReconciliationReport> ComputeAsync(CancellationToken ct);

    /// <summary>
    /// Stores a computed report as the latest one.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task SaveAsync(ReconciliationReport report, CancellationToken ct);

    /// <summary>
    /// Retrieves the latest stored report, or <c>null</c> if none was computed yet.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ReconciliationReport?> GetLatestAsync(CancellationToken ct);
}
