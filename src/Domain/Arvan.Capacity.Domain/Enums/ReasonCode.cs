using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Why a reservation reached its state. The shortage members are ordered by severity, so the most severe of several is the numerically largest.
/// </summary>
public enum ReasonCode
{
    /// <summary>
    /// The hold ran past its lease deadline.
    /// </summary>
    [Display(Name = "HoldExpired")]
    HoldExpired = 1,

    /// <summary>
    /// The client cancelled.
    /// </summary>
    [Display(Name = "CancelledByClient")]
    CancelledByClient = 2,

    /// <summary>
    /// The wait ran past its deadline.
    /// </summary>
    [Display(Name = "WaitTimeout")]
    WaitTimeout = 3,

    /// <summary>
    /// The customer's contract limit, counting its open holds, would be exceeded.
    /// </summary>
    [Display(Name = "QuotaExceeded")]
    QuotaExceeded = 5,

    /// <summary>
    /// The customer's share of a scarce pool would exceed the product's cap.
    /// </summary>
    [Display(Name = "PoolShareExceeded")]
    PoolShareExceeded = 6,

    /// <summary>
    /// Short only because of open holds or an offline pool.
    /// </summary>
    [Display(Name = "CapacityTemporarilyUnavailable")]
    CapacityTemporarilyUnavailable = 10,

    /// <summary>
    /// The request fits registered capacity but live allocations fill it.
    /// </summary>
    [Display(Name = "CapacitySoldOut")]
    CapacitySoldOut = 11,

    /// <summary>
    /// More than any pool in scope can ever provide.
    /// </summary>
    [Display(Name = "ExceedsRegisteredCapacity")]
    ExceedsRegisteredCapacity = 12,
}
