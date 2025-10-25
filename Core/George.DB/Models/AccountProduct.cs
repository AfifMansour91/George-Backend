using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProduct")]
public partial class AccountProduct
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public long ProductTemplateId { get; set; }

    public bool IsEnabled { get; set; }

    [StringLength(30)]
    public string EditingStatus { get; set; } = null!;

    [StringLength(300)]
    public string? Title { get; set; }

    [StringLength(1000)]
    public string? ShortDescription { get; set; }

    public string? DescriptionHtml { get; set; }

    public int? BrandId { get; set; }

    public int? SupplierId { get; set; }

    public bool? IsKosher { get; set; }

    public int? KosherStatusId { get; set; }

    public int? WeightPricingModelId { get; set; }

    public bool? ShowPricePer100g { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? BaseUnitPrice { get; set; }

    public int? BaseUnitId { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? BaseWeightGrams { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? StockQuantity { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? WeightStepGrams { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountProducts")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("AccountProduct")]
    public virtual ICollection<AccountProductAttribute> AccountProductAttributes { get; set; } = new List<AccountProductAttribute>();

    [InverseProperty("AccountProduct")]
    public virtual ICollection<AccountProductCategory> AccountProductCategories { get; set; } = new List<AccountProductCategory>();

    [InverseProperty("AccountProduct")]
    public virtual ICollection<AccountProductMedium> AccountProductMedia { get; set; } = new List<AccountProductMedium>();

    [InverseProperty("AccountProduct")]
    public virtual ICollection<AccountProductVariant> AccountProductVariants { get; set; } = new List<AccountProductVariant>();

    [ForeignKey("BaseUnitId")]
    [InverseProperty("AccountProducts")]
    public virtual Unit? BaseUnit { get; set; }

    [ForeignKey("BrandId")]
    [InverseProperty("AccountProducts")]
    public virtual Brand? Brand { get; set; }

    [InverseProperty("AccountProduct")]
    public virtual ICollection<EcomProductMap> EcomProductMaps { get; set; } = new List<EcomProductMap>();

    [ForeignKey("KosherStatusId")]
    [InverseProperty("AccountProducts")]
    public virtual KosherStatus? KosherStatus { get; set; }

    [InverseProperty("AccountProduct")]
    public virtual ICollection<ProductEditLog> ProductEditLogs { get; set; } = new List<ProductEditLog>();

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("AccountProducts")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;

    [ForeignKey("SupplierId")]
    [InverseProperty("AccountProducts")]
    public virtual Supplier? Supplier { get; set; }

    [ForeignKey("WeightPricingModelId")]
    [InverseProperty("AccountProducts")]
    public virtual WeightPricingModel? WeightPricingModel { get; set; }
}
