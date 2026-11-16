using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// The kind of movement an append-only ledger row records.
/// </summary>
public enum LedgerEntryType
{
    /// <summary>
    /// Units moved from free to held.
    /// </summary>
    [Display(Name = "Hold")]
    Hold = 1,

    /// <summary>
    /// Units moved from held to used.
    /// </summary>
    [Display(Name = "Commit")]
    Commit = 2,

    /// <summary>
    /// Held units went back to free.
    /// </summary>
    [Display(Name = "Return")]
    Return = 3,

    /// <summary>
    /// Used units went back to free.
    /// </summary>
    [Display(Name = "Release")]
    Release = 4,

    /// <summary>
    /// An operator changed capacity directly.
    /// </summary>
    [Display(Name = "Adjust")]
    Adjust = 5,
}
