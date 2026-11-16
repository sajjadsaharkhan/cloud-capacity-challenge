using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// What a customer is told about the capacity of a product in a region: a level, never numbers.
/// Ordered from worst to best, so the worst of several is the smallest.
/// </summary>
public enum CapacityLevel
{
    /// <summary>
    /// No pool has free capacity for at least one of the product's classes.
    /// </summary>
    [Display(Name = "Unavailable")]
    Unavailable = 1,

    /// <summary>
    /// Free capacity is below a tenth of what can be sold.
    /// </summary>
    [Display(Name = "Limited")]
    Limited = 2,

    /// <summary>
    /// Plenty of free capacity.
    /// </summary>
    [Display(Name = "Available")]
    Available = 3,
}
