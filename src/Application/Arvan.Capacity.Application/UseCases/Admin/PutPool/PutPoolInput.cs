using Arvan.Capacity.Domain.Common;

namespace Arvan.Capacity.Application.UseCases.Admin.PutPool;

/// <summary>
/// The input to <see cref="PutPoolUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="Id">The pool's id.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone, if any.</param>
/// <param name="HostRef">The host, for a host-scoped pool.</param>
public sealed record PutPoolInput(Actor Actor, string IdempotencyKey, int Id, string Region, string? Zone, string? HostRef);
