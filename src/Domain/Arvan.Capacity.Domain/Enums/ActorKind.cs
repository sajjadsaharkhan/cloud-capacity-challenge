using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Who caused a state change.
/// </summary>
public enum ActorKind
{
    /// <summary>
    /// A customer, through the panel or automation.
    /// </summary>
    [Display(Name = "Customer")]
    Customer = 1,

    /// <summary>
    /// An internal product service acting for a customer.
    /// </summary>
    [Display(Name = "Service")]
    Service = 2,

    /// <summary>
    /// A capacity operator.
    /// </summary>
    [Display(Name = "Admin")]
    Admin = 3,

    /// <summary>
    /// The background expiry process.
    /// </summary>
    [Display(Name = "Sweeper")]
    Sweeper = 4,
}
