using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// What a submit asks for when capacity is short.
/// </summary>
public enum ShortagePolicy
{
    /// <summary>
    /// Reject the request.
    /// </summary>
    [Display(Name = "Reject")]
    Reject = 1,

    /// <summary>
    /// Wait, for a bounded time, when the shortage is temporary only.
    /// </summary>
    [Display(Name = "Wait")]
    Wait = 2,
}
