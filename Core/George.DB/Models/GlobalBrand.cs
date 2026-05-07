using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("ParentGlobalBrandId", Name = "IX_GlobalBrand_ParentGlobalBrandId")]
public partial class GlobalBrand
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string? Slug { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public int? ParentGlobalBrandId { get; set; }

    public int? SortOrder { get; set; }

    public int? ProductCount { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? IconUrl { get; set; }

    [StringLength(200)]
    public string? SeoTitle { get; set; }

    [StringLength(500)]
    public string? SeoDescription { get; set; }

    /// <summary>WooCommerce taxonomy term id; null if not synced yet.</summary>
    public int? WooCommerceBrandId { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("GlobalBrandCreationUser")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("GlobalBrandUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("ParentGlobalBrand")]
    public virtual ICollection<GlobalBrand> InverseParentGlobalBrand { get; set; } = new List<GlobalBrand>();

    [ForeignKey("ParentGlobalBrandId")]
    [InverseProperty("InverseParentGlobalBrand")]
    public virtual GlobalBrand? ParentGlobalBrand { get; set; }

    /// <summary>Local Brand rows that were copied down from this GlobalBrand.</summary>
    [InverseProperty("SourceGlobalBrand")]
    public virtual ICollection<Brand> Brand { get; set; } = new List<Brand>();

    /// <summary>Many-to-many TemplateProduct &lt;-&gt; GlobalBrand via TemplateProductBrand.</summary>
    [InverseProperty("GlobalBrand")]
    public virtual ICollection<TemplateProductBrand> TemplateProductBrand { get; set; } = new List<TemplateProductBrand>();
}
