using System.ComponentModel.DataAnnotations;

namespace Arvan.Capacity.Domain.Enums;

/// <summary>
/// A kind of sellable capacity, counted in the base unit named on each member.
/// </summary>
public enum ResourceClass
{
    /// <summary>
    /// Virtual CPU, in millicores.
    /// </summary>
    [Display(Name = "Vcpu")]
    Vcpu = 1,

    /// <summary>
    /// Memory, in megabytes.
    /// </summary>
    [Display(Name = "RamMb")]
    RamMb = 2,

    /// <summary>
    /// Disk, in gigabytes.
    /// </summary>
    [Display(Name = "DiskGb")]
    DiskGb = 3,

    /// <summary>
    /// Public IPv4 addresses, in units.
    /// </summary>
    [Display(Name = "Ipv4")]
    Ipv4 = 4,

    /// <summary>
    /// GPUs, in units.
    /// </summary>
    [Display(Name = "Gpu")]
    Gpu = 5,
}
