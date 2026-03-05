using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

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

    public int? WeightTolerancePercent { get; set; }

    public bool? DepreciationEnabled { get; set; }

    [StringLength(200)]
    public string? DepreciationPercentagesJson { get; set; }

    public int? PrepTimeMinutes { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShippingCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? FreeShippingAbove { get; set; }

    public bool? AutoPrintEnabled { get; set; }

    public bool? PrintNewOrderImmediate { get; set; }

    public bool? PrintMovedToTreatment { get; set; }

    public bool? PrintFutureImmediate { get; set; }

    public bool? PrintFutureAtTimeEnabled { get; set; }

    [StringLength(10)]
    public string? PrintFutureAtTime { get; set; }

    /// <summary>Voucher printer: print without showing confirmation (use default printer).</summary>
    public bool? VoucherPrinterSilent { get; set; }

    /// <summary>Voucher printer: display name / connection label (e.g. WIFI, iPad).</summary>
    [StringLength(100)]
    public string? VoucherPrinterName { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Site")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Site")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [InverseProperty("Site")]
    public virtual ICollection<AccountWizardStepData> AccountWizardStepData { get; set; } = new List<AccountWizardStepData>();

    [InverseProperty("Site")]
    public virtual ICollection<Attribute> Attribute { get; set; } = new List<Attribute>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("SiteCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Site")]
    public virtual ICollection<Order> Order { get; set; } = new List<Order>();

    [InverseProperty("Site")]
    public virtual ICollection<SiteOrderReceptionClosed> SiteOrderReceptionClosed { get; set; } = new List<SiteOrderReceptionClosed>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("SiteUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<BusinessType> BusinessType { get; set; } = new List<BusinessType>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<Category> Category { get; set; } = new List<Category>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<TemplateAttribute> TemplateAttribute { get; set; } = new List<TemplateAttribute>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<User> User { get; set; } = new List<User>();
}
