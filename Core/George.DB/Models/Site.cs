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
    public long Id { get; set; }

    public long AccountId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Location { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(250)]
    public string? ContactEmail { get; set; }

    [StringLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsKosherSite { get; set; }

    public bool AllowWeightedProducts { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Sites")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Site")]
    public virtual ICollection<AccountCategorySite> AccountCategorySites { get; set; } = new List<AccountCategorySite>();

    [InverseProperty("Site")]
    public virtual ICollection<AccountProductSite> AccountProductSites { get; set; } = new List<AccountProductSite>();

    [InverseProperty("Site")]
    public virtual ICollection<AccountUser> AccountUsers { get; set; } = new List<AccountUser>();

    [InverseProperty("Site")]
    public virtual ICollection<ProductTemplateAttributeSite> ProductTemplateAttributeSites { get; set; } = new List<ProductTemplateAttributeSite>();

    [InverseProperty("Site")]
    public virtual ICollection<SiteBusinessType> SiteBusinessTypes { get; set; } = new List<SiteBusinessType>();
}
