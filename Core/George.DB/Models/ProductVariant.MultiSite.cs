using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace George.DB;

/// <summary>MultiSite Phase 2 additions to ProductVariant (per-site stock navigation).</summary>
public partial class ProductVariant
{
    [InverseProperty("ProductVariant")]
    public virtual ICollection<ProductSiteVariantStock> ProductSiteVariantStock { get; set; } = new List<ProductSiteVariantStock>();
}
