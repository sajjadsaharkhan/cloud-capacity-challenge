using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Whether a product can be reserved.
/// </summary>
public enum ProductStatus
{
    /// <summary>
    /// Open for reservations.
    /// </summary>
    [Display(Name = "Active")]
    Active = 1,

    /// <summary>
    /// No longer reservable.
    /// </summary>
    [Display(Name = "Retired")]
    Retired = 2,
}
