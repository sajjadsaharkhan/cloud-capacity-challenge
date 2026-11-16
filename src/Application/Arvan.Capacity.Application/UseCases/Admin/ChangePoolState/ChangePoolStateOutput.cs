using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;

/// <summary>
/// The output of <see cref="ChangePoolStateUseCase"/>.
/// </summary>
/// <param name="Id">The pool's id.</param>
/// <param name="AdminState">The state after the change.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record ChangePoolStateOutput(int Id, PoolAdminState AdminState, bool Replayed);
