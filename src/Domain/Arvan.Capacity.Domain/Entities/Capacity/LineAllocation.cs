using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// A quantity of one resource class bound to the pool it moves in. The unit the capacity ledger
/// applies, whichever way the quantity moves.
/// </summary>
/// <param name="PoolId">The pool.</param>
/// <param name="ResourceClass">The class.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record LineAllocation(int PoolId, ResourceClass ResourceClass, long Quantity);
