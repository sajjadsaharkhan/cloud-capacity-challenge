using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Products;

namespace Arvan.Capacity.Application.UseCases.Admin.PutProduct;

/// <summary>
/// The input to <see cref="PutProductUseCase"/>.
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="Code">The product code.</param>
/// <param name="Definition">The product's status, classes and policy.</param>
public sealed record PutProductInput(Actor Actor, string IdempotencyKey, string Code, ProductDefinition Definition);
