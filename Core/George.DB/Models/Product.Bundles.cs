using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace George.DB;

/// <summary>
/// Bundles (מארזים) navigations of Product, kept in a separate partial so the scaffolded Product.cs
/// can be regenerated without losing them. Only meaningful when SetupType is 'bundle'.
/// </summary>
public partial class Product
{
    [InverseProperty("Product")]
    public virtual ProductBundleConfig? BundleConfig { get; set; }

    [InverseProperty("BundleProduct")]
    public virtual ICollection<ProductBundleComponent> BundleComponents { get; set; } = new List<ProductBundleComponent>();
}
