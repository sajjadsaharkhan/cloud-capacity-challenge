using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// The transition an event row records.
/// </summary>
public enum ReservationEventType
{
    /// <summary>
    /// A reservation was submitted.
    /// </summary>
    [Display(Name = "Submit")]
    Submit = 1,

    /// <summary>
    /// A waiting reservation got its capacity.
    /// </summary>
    [Display(Name = "Promote")]
    Promote = 2,

    /// <summary>
    /// A hold was confirmed.
    /// </summary>
    [Display(Name = "Confirm")]
    Confirm = 3,

    /// <summary>
    /// A hold's lease was extended.
    /// </summary>
    [Display(Name = "Extend")]
    Extend = 4,

    /// <summary>
    /// A reservation was cancelled.
    /// </summary>
    [Display(Name = "Cancel")]
    Cancel = 5,

    /// <summary>
    /// A hold ran past its deadline.
    /// </summary>
    [Display(Name = "Expire")]
    Expire = 6,

    /// <summary>
    /// A wait ran past its deadline.
    /// </summary>
    [Display(Name = "Timeout")]
    Timeout = 7,

    /// <summary>
    /// Allocated capacity was released.
    /// </summary>
    [Display(Name = "Release")]
    Release = 8,
}
