using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutPool;

/// <summary>
/// The output of <see cref="PutPoolUseCase"/>.
/// </summary>
/// <param name="Id">The pool's id.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone.</param>
/// <param name="HostRef">The host.</param>
/// <param name="AdminState">The pool's administrative state.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record PutPoolOutput(int Id, string Region, string? Zone, string? HostRef, PoolAdminState AdminState, bool Replayed);
