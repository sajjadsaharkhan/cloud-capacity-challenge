using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.PutQuota;

/// <summary>
/// The output of <see cref="PutQuotaUseCase"/>.
/// </summary>
/// <param name="CustomerId">The customer.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Region">The region.</param>
/// <param name="Limit">The limit stored.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record PutQuotaOutput(string CustomerId, ResourceClass ResourceClass, string Region, long Limit, bool Replayed);
