using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// Why a product service released allocated capacity.
/// </summary>
public enum ReleaseReason
{
    /// <summary>
    /// The service the capacity backed was deleted.
    /// </summary>
    [Display(Name = "ServiceDeleted")]
    ServiceDeleted = 1,

    /// <summary>
    /// Provisioning failed after the hold was confirmed.
    /// </summary>
    [Display(Name = "ProvisioningFailed")]
    ProvisioningFailed = 2,

    /// <summary>
    /// A reconciler found the allocation orphaned.
    /// </summary>
    [Display(Name = "Reconciled")]
    Reconciled = 3,
}
