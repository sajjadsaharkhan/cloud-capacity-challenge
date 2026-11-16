using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutInventory;

/// <summary>
/// The input to <see cref="PutInventoryUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="PoolId">The pool.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Configuration">The configuration to apply.</param>
/// <param name="Force">When the change is below current usage: take the pool offline instead of refusing.</param>
public sealed record PutInventoryInput(
    Actor Actor, string IdempotencyKey, int PoolId, ResourceClass ResourceClass, InventoryConfiguration Configuration, bool Force);
