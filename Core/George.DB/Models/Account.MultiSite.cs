using System.ComponentModel.DataAnnotations;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 additions to Account. ManagementMode drives the ongoing "all sites" working mode.
/// </summary>
public partial class Account
{
    /// <summary>'separate' (each branch managed independently) or 'network' (all-sites mode + propagation). Null = derive from WizardType.</summary>
    [StringLength(20)]
    public string? ManagementMode { get; set; }
}
