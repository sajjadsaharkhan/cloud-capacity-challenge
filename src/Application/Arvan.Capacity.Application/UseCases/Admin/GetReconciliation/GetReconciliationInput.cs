using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.GetReconciliation;

/// <summary>
/// The input to <see cref="GetReconciliationUseCase"/>.
/// </summary>
/// <param name="ActorKind">Who is asking; must be an admin.</param>
/// <param name="Refresh">Whether to compute the report now instead of returning the latest stored one.</param>
public sealed record GetReconciliationInput(ActorKind ActorKind, bool Refresh);
