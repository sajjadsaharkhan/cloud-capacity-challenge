using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Reconciliation;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.GetReconciliation;

/// <summary>
/// Returns the reconciliation report: counters against the ledger and the reservations, the
/// unsettled backlog and the outbox backlog. Corresponds to <c>GET /v1/admin/reconciliation</c>.
/// </summary>
public sealed class GetReconciliationUseCase(IReconciliationOperations reconciliation)
    : IUseCase<GetReconciliationInput, ReconciliationReport>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public Task<ReconciliationReport> ExecuteAsync(GetReconciliationInput input, CancellationToken ct) =>
        input.ActorKind is not ActorKind.Admin
            ? throw new ActorNotAllowedException("read the reconciliation report")
            : input.Refresh ? reconciliation.ComputeAsync(ct) : reconciliation.GetLatestAsync(ct);
}
