using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Reservations;

/// <summary>
/// One resource line of a <see cref="Reservation"/>: a quantity of one resource class, drawn from
/// one pool once the reservation is held.
/// </summary>
public sealed class ReservationLine
{
    /// <summary>
    /// The resource class this line asks for.
    /// </summary>
    public ResourceClass ResourceClass { get; private set; }

    /// <summary>
    /// The quantity in the class's base unit. Always positive.
    /// </summary>
    public long Quantity { get; private set; }

    /// <summary>
    /// The pool the quantity is held in. <c>null</c> until the reservation is held, and for good on
    /// a rejected one.
    /// </summary>
    public int? PoolId { get; private set; }

    /// <summary>
    /// Reserved for materialization by the persistence layer.
    /// </summary>
    private ReservationLine()
    {
    }

    /// <summary>
    /// Creates a line.
    /// </summary>
    /// <param name="resourceClass">The resource class asked for.</param>
    /// <param name="quantity">The quantity in the class's base unit.</param>
    /// <param name="poolId">The pool holding it, or <c>null</c> when nothing is held.</param>
    public ReservationLine(ResourceClass resourceClass, long quantity, int? poolId = null)
    {
        ResourceClass = resourceClass;
        Quantity = quantity;
        PoolId = poolId;
    }
}
