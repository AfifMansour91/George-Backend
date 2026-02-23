using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Site")]
public partial class Site
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

    public int AccountId { get; set; }

    [StringLength(200)]
    public string SiteName { get; set; } = null!;

    [StringLength(500)]
    public string? Location { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [StringLength(250)]
    public string? ContactEmail { get; set; }

    [StringLength(50)]
    public string? ContactPhone { get; set; }

    public bool? IsKosherSite { get; set; }

    public bool? AllowWeightedProducts { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = null!;

    [StringLength(500)]
    public string? WooCommerceUrl { get; set; }

    [StringLength(250)]
    public string? WooCommerceKey { get; set; }

    [StringLength(250)]
    public string? WooCommerceSecret { get; set; }

    public bool? WooCommerceEnabled { get; set; }

    // Shop settings (Sprint 2)
    public int? WeightTolerancePercent { get; set; }
    public bool? DepreciationEnabled { get; set; }
    [StringLength(200)]
    public string? DepreciationPercentagesJson { get; set; }
    public int? PrepTimeMinutes { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ShippingCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FreeShippingAbove { get; set; }
    public bool? AutoPrintEnabled { get; set; }
    public bool? PrintNewOrderImmediate { get; set; }
    public bool? PrintMovedToTreatment { get; set; }
    public bool? PrintFutureImmediate { get; set; }
    public bool? PrintFutureAtTimeEnabled { get; set; }
    [StringLength(10)]
    public string? PrintFutureAtTime { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Sites")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Site")]
    public virtual ICollection<Attribute> Attributes { get; set; } = new List<Attribute>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("SiteCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("SiteUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<BusinessType> BusinessTypes { get; set; } = new List<BusinessType>();

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<TemplateAttribute> TemplateAttributes { get; set; } = new List<TemplateAttribute>();

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();

    [ForeignKey("SiteId")]
    [InverseProperty("Sites")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
