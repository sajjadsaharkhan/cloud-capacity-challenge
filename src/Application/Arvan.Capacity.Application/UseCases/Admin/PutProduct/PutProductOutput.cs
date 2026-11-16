using Arvan.Capacity.Domain.Entities.Products;

namespace Arvan.Capacity.Application.UseCases.Admin.PutProduct;

/// <summary>
/// The output of <see cref="PutProductUseCase"/>.
/// </summary>
/// <param name="Code">The product code.</param>
/// <param name="Definition">The definition stored.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record PutProductOutput(string Code, ProductDefinition Definition, bool Replayed);
