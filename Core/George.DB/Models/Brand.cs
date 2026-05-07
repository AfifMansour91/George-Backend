using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Brand_AccountId")]
[Index("ParentBrandId", Name = "IX_Brand_ParentBrandId")]
[Index("SourceGlobalBrandId", Name = "IX_Brand_SourceGlobalBrandId")]
public partial class Brand
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public int? AccountId { get; set; }

    [StringLength(200)]
    public string? Slug { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? IconUrl { get; set; }

    public int? ParentBrandId { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsEnabled { get; set; }

    [StringLength(200)]
    public string? SeoTitle { get; set; }

    [StringLength(500)]
    public string? SeoDescription { get; set; }

    /// <summary>WooCommerce taxonomy term id; null if not synced yet.</summary>
    public int? WooCommerceBrandId { get; set; }

    /// <summary>If this brand was copied down from a GlobalBrand, the source id; null otherwise.</summary>
    public int? SourceGlobalBrandId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Brand")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("BrandCreationUser")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("BrandUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("ParentBrand")]
    public virtual ICollection<Brand> InverseParentBrand { get; set; } = new List<Brand>();

    [ForeignKey("ParentBrandId")]
    [InverseProperty("InverseParentBrand")]
    public virtual Brand? ParentBrand { get; set; }

    [ForeignKey("SourceGlobalBrandId")]
    [InverseProperty("Brand")]
    public virtual GlobalBrand? SourceGlobalBrand { get; set; }

    /// <summary>
    /// Existing single-FK collection (Product.BrandId). Kept for back-compat for one release;
    /// new code should use the <see cref="ProductBrand"/> join.
    /// </summary>
    [InverseProperty("Brand")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    /// <summary>Same back-compat note as <see cref="Product"/>.</summary>
    [InverseProperty("Brand")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();

    /// <summary>Many-to-many Product &lt;-&gt; Brand via ProductBrand.</summary>
    [InverseProperty("Brand")]
    public virtual ICollection<ProductBrand> ProductBrand { get; set; } = new List<ProductBrand>();

    /// <summary>Many-to-many Brand &lt;-&gt; Site via shadow join "BrandSite".</summary>
    [ForeignKey("BrandId")]
    [InverseProperty("Brand")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();
}
