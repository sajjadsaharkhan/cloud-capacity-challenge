using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutQuota;

/// <summary>
/// The input to <see cref="PutQuotaUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="CustomerId">The customer.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Region">The region.</param>
/// <param name="Limit">The contract limit.</param>
public sealed record PutQuotaInput(
    Actor Actor, string IdempotencyKey, string CustomerId, ResourceClass ResourceClass, string Region, long Limit);
