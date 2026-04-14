using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Product_AccountId")]
public partial class Product
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

    public bool IsActive { get; set; }

    [StringLength(300)]
    public string Name { get; set; } = null!;

    [StringLength(2000)]
    public string? ShortDescription { get; set; }

    public string? LongDescription { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Price { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SalePrice { get; set; }

    [Precision(0)]
    public DateTime? SalePriceStartDate { get; set; }

    [Precision(0)]
    public DateTime? SalePriceEndDate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? CostPrice { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    public int? StockManagementTypeId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? StockQuantity { get; set; }

    public int? StockStatusId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Weight { get; set; }

    public int? ShippingClassId { get; set; }

    public int? StatusId { get; set; }

    public int? VisibilityId { get; set; }

    public int? SetupTypeId { get; set; }

    public int? BrandId { get; set; }

    public int? SupplierId { get; set; }

    public bool? IsKosher { get; set; }

    public bool? IsWeighted { get; set; }

    public int? WeightConfigId { get; set; }

    public bool? ShowAsMl { get; set; }

    [StringLength(5)]
    public string? WeightUnit { get; set; }

    [StringLength(300)]
    public string? SeoTitle { get; set; }

    [StringLength(2000)]
    public string? SeoDescription { get; set; }

    [StringLength(100)]
    public string? TemplateId { get; set; }

    [StringLength(100)]
    public string? SourceProductId { get; set; }

    public int? AccountId { get; set; }

    public int? WooCommerceId { get; set; }

    public int? DisplayOrder { get; set; }

    /// <summary>When stock is managed per variation: if true, use quantity per variation in Woo; if false, in/out only (no per-variation manage_stock quantity).</summary>
    public bool? VariationStockByQuantity { get; set; }

    /// <summary>Per-product low-stock threshold. When set (> 0), overrides the account-level default for low-stock coloring and filtering.</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? LowStockThreshold { get; set; }

    [ForeignKey("BrandId")]
    [InverseProperty("Product")]
    public virtual Brand? Brand { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("ProductCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<ProductCategory> ProductCategory { get; set; } = new List<ProductCategory>();

    [InverseProperty("Product")]
    public virtual ICollection<ProductImage> ProductImage { get; set; } = new List<ProductImage>();

    [InverseProperty("Product")]
    public virtual ICollection<ProductOption> ProductOption { get; set; } = new List<ProductOption>();

    [InverseProperty("Product")]
    public virtual ICollection<ProductVariant> ProductVariant { get; set; } = new List<ProductVariant>();

    [ForeignKey("SetupTypeId")]
    [InverseProperty("Product")]
    public virtual SetupType? SetupType { get; set; }

    [ForeignKey("ShippingClassId")]
    [InverseProperty("Product")]
    public virtual ShippingClass? ShippingClass { get; set; }

    [ForeignKey("StatusId")]
    [InverseProperty("Product")]
    public virtual ProductStatus? Status { get; set; }

    [ForeignKey("StockManagementTypeId")]
    [InverseProperty("Product")]
    public virtual StockManagementType? StockManagementType { get; set; }

    [ForeignKey("StockStatusId")]
    [InverseProperty("Product")]
    public virtual StockStatus? StockStatus { get; set; }

    [ForeignKey("SupplierId")]
    [InverseProperty("Product")]
    public virtual Supplier? Supplier { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("ProductUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("VisibilityId")]
    [InverseProperty("Product")]
    public virtual Visibility? Visibility { get; set; }

    [ForeignKey("WeightConfigId")]
    [InverseProperty("Product")]
    public virtual WeightConfig? WeightConfig { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductNavigation")]
    public virtual ICollection<Product> ComplementaryProduct { get; set; } = new List<Product>();

    [ForeignKey("RelatedProductId")]
    [InverseProperty("RelatedProduct")]
    public virtual ICollection<Product> Product1 { get; set; } = new List<Product>();

    [ForeignKey("ComplementaryProductId")]
    [InverseProperty("ComplementaryProduct")]
    public virtual ICollection<Product> ProductNavigation { get; set; } = new List<Product>();

    [ForeignKey("ProductId")]
    [InverseProperty("Product1")]
    public virtual ICollection<Product> RelatedProduct { get; set; } = new List<Product>();

    [ForeignKey("ProductId")]
    [InverseProperty("Product")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();

    [ForeignKey("ProductId")]
    [InverseProperty("Product")]
    public virtual ICollection<Tag> Tag { get; set; } = new List<Tag>();
}
