using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class TemplateProduct
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

    [StringLength(100)]
    public string? TemplateId { get; set; }

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

    public int? StockQuantity { get; set; }

    public int? StockStatusId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Weight { get; set; }

    public int? ShippingClassId { get; set; }

    public int? StatusId { get; set; }

    public int? VisibilityId { get; set; }

    public int? BrandId { get; set; }

    public int? SupplierId { get; set; }

    public bool? IsKosher { get; set; }

    public bool? IsWeighted { get; set; }

    public int? SetupTypeId { get; set; }

    public int? WeightConfigId { get; set; }

    public bool? ShowAsMl { get; set; }

    [StringLength(5)]
    public string? WeightUnit { get; set; }

    [StringLength(300)]
    public string? SeoTitle { get; set; }

    [StringLength(2000)]
    public string? SeoDescription { get; set; }

    [StringLength(100)]
    public string? SourceProductId { get; set; }

    public int? DisplayOrder { get; set; }

    [ForeignKey("BrandId")]
    [InverseProperty("TemplateProduct")]
    public virtual Brand? Brand { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("TemplateProductCreationUser")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("SetupTypeId")]
    [InverseProperty("TemplateProduct")]
    public virtual SetupType? SetupType { get; set; }

    [ForeignKey("ShippingClassId")]
    [InverseProperty("TemplateProduct")]
    public virtual ShippingClass? ShippingClass { get; set; }

    [ForeignKey("StatusId")]
    [InverseProperty("TemplateProduct")]
    public virtual ProductStatus? Status { get; set; }

    [ForeignKey("StockManagementTypeId")]
    [InverseProperty("TemplateProduct")]
    public virtual StockManagementType? StockManagementType { get; set; }

    [ForeignKey("StockStatusId")]
    [InverseProperty("TemplateProduct")]
    public virtual StockStatus? StockStatus { get; set; }

    [ForeignKey("SupplierId")]
    [InverseProperty("TemplateProduct")]
    public virtual Supplier? Supplier { get; set; }

    [InverseProperty("TemplateProduct")]
    public virtual ICollection<TemplateProductBrand> TemplateProductBrand { get; set; } = new List<TemplateProductBrand>();

    [InverseProperty("TemplateProduct")]
    public virtual ICollection<TemplateProductCategory> TemplateProductCategory { get; set; } = new List<TemplateProductCategory>();

    [InverseProperty("TemplateProduct")]
    public virtual ICollection<TemplateProductImage> TemplateProductImage { get; set; } = new List<TemplateProductImage>();

    [InverseProperty("TemplateProduct")]
    public virtual ICollection<TemplateProductOption> TemplateProductOption { get; set; } = new List<TemplateProductOption>();

    [InverseProperty("TemplateProduct")]
    public virtual ICollection<TemplateProductVariant> TemplateProductVariant { get; set; } = new List<TemplateProductVariant>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("TemplateProductUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("VisibilityId")]
    [InverseProperty("TemplateProduct")]
    public virtual Visibility? Visibility { get; set; }

    [ForeignKey("WeightConfigId")]
    [InverseProperty("TemplateProduct")]
    public virtual WeightConfig? WeightConfig { get; set; }

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductNavigation")]
    public virtual ICollection<TemplateProduct> ComplementaryTemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProduct1")]
    public virtual ICollection<TemplateProduct> RelatedTemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProduct")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProduct")]
    public virtual ICollection<Tag> Tag { get; set; } = new List<Tag>();

    [ForeignKey("RelatedTemplateProductId")]
    [InverseProperty("RelatedTemplateProduct")]
    public virtual ICollection<TemplateProduct> TemplateProduct1 { get; set; } = new List<TemplateProduct>();

    [ForeignKey("ComplementaryTemplateProductId")]
    [InverseProperty("ComplementaryTemplateProduct")]
    public virtual ICollection<TemplateProduct> TemplateProductNavigation { get; set; } = new List<TemplateProduct>();
}
