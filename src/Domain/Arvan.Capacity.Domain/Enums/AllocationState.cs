using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Whether a confirmed reservation still occupies capacity.
/// </summary>
public enum AllocationState
{
    /// <summary>
    /// The confirmed capacity is still allocated.
    /// </summary>
    [Display(Name = "Active")]
    Active = 1,

    /// <summary>
    /// The product service gave the capacity back.
    /// </summary>
    [Display(Name = "Released")]
    Released = 2,
}
