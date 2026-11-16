using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// The request state machine's state, as stored and as read.
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// Waiting, by the customer's choice, for capacity that is short only temporarily. Holds nothing.
    /// </summary>
    [Display(Name = "Waiting")]
    Waiting = 1,

    /// <summary>
    /// Capacity is held until the lease deadline. Counted in the inventory's held counter.
    /// </summary>
    [Display(Name = "Held")]
    Held = 2,

    /// <summary>
    /// The hold was confirmed. Capacity is allocated until it is released.
    /// </summary>
    [Display(Name = "Confirmed")]
    Confirmed = 3,

    /// <summary>
    /// The request could not be served. Terminal.
    /// </summary>
    [Display(Name = "Rejected")]
    Rejected = 4,

    /// <summary>
    /// Cancelled by the client before its deadline. Terminal.
    /// </summary>
    [Display(Name = "Cancelled")]
    Cancelled = 5,

    /// <summary>
    /// The hold ran past its lease deadline. Terminal.
    /// </summary>
    [Display(Name = "Expired")]
    Expired = 6,
}
