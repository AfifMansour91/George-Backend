using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductTemplate")]
public partial class ProductTemplate
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    [StringLength(300)]
    public string Title { get; set; } = null!;

    [StringLength(1000)]
    public string? ShortDescription { get; set; }

    public string? DescriptionHtml { get; set; }

    public int? BrandId { get; set; }

    public int? SupplierId { get; set; }

    public int ProductTypeId { get; set; }

    public int? WeightPricingModelId { get; set; }

    public bool IsKosherDefault { get; set; }

    public int? KosherStatusId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? BaseUnitPrice { get; set; }

    public int? BaseUnitId { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? BaseWeightGrams { get; set; }

    public bool ShowPricePer100g { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("MatchedProductTemplate")]
    public virtual ICollection<AccountProductImportStaging> AccountProductImportStagings { get; set; } = new List<AccountProductImportStaging>();

    [InverseProperty("ProductTemplate")]
    public virtual ICollection<AccountProduct> AccountProducts { get; set; } = new List<AccountProduct>();

    [ForeignKey("BaseUnitId")]
    [InverseProperty("ProductTemplates")]
    public virtual Unit? BaseUnit { get; set; }

    [ForeignKey("BrandId")]
    [InverseProperty("ProductTemplates")]
    public virtual Brand? Brand { get; set; }

    [ForeignKey("KosherStatusId")]
    [InverseProperty("ProductTemplates")]
    public virtual KosherStatus? KosherStatus { get; set; }

    [InverseProperty("ProductTemplate")]
    public virtual ICollection<ProductTemplateAttribute> ProductTemplateAttributes { get; set; } = new List<ProductTemplateAttribute>();

    [InverseProperty("ProductTemplate")]
    public virtual ICollection<ProductTemplateMedium> ProductTemplateMedia { get; set; } = new List<ProductTemplateMedium>();

    [InverseProperty("ProductTemplate")]
    public virtual ICollection<ProductTemplateSelectableWeight> ProductTemplateSelectableWeights { get; set; } = new List<ProductTemplateSelectableWeight>();

    [ForeignKey("ProductTypeId")]
    [InverseProperty("ProductTemplates")]
    public virtual ProductType ProductType { get; set; } = null!;

    [ForeignKey("SupplierId")]
    [InverseProperty("ProductTemplates")]
    public virtual Supplier? Supplier { get; set; }

    [ForeignKey("WeightPricingModelId")]
    [InverseProperty("ProductTemplates")]
    public virtual WeightPricingModel? WeightPricingModel { get; set; }

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplates")]
    public virtual ICollection<BusinessType> BusinessTypes { get; set; } = new List<BusinessType>();

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplates")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
