using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// The administrative state of a pool of capacity; it decides where idle units sit.
/// </summary>
public enum PoolAdminState
{
    /// <summary>
    /// Known but not yet sold.
    /// </summary>
    [Display(Name = "Registered")]
    Registered = 1,

    /// <summary>
    /// Open for new holds.
    /// </summary>
    [Display(Name = "Available")]
    Available = 2,

    /// <summary>
    /// Taken out of sale. Existing holds and allocations finish their own cycle.
    /// </summary>
    [Display(Name = "Offline")]
    Offline = 3,

    /// <summary>
    /// Permanently removed. Terminal.
    /// </summary>
    [Display(Name = "Retired")]
    Retired = 4,
}
