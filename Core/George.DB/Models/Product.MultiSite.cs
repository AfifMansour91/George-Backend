using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 additions to Product (kept in a separate partial so the scaffolded Product.cs
/// can be regenerated without losing these). See MultiSite override contract.
/// </summary>
public partial class Product
{
    /// <summary>'network' (canonical, shared across sites) or 'local' (managed only on OwnerSiteId). Null = treated as 'network'.</summary>
    [StringLength(20)]
    public string? ManagementMode { get; set; }

    /// <summary>When ManagementMode = 'local': the single site that owns/manages this product.</summary>
    public int? OwnerSiteId { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<ProductSiteOverride> ProductSiteOverride { get; set; } = new List<ProductSiteOverride>();
}
