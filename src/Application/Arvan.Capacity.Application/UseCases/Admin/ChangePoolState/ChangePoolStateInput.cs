using Arvan.Capacity.Domain.Common;

namespace Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;

/// <summary>
/// The input to <see cref="ChangePoolStateUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; an admin, or a service acting as the health checker for take_offline and restore.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="Id">The pool's id.</param>
/// <param name="Event">One of <c>activate</c>, <c>take_offline</c>, <c>restore</c>, <c>retire</c>.</param>
public sealed record ChangePoolStateInput(Actor Actor, string IdempotencyKey, int Id, string Event);
