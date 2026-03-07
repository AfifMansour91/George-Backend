using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>CRM: Customer per site. One record per (SiteId, NormalizedPhone). Same person at another site = separate row. Delete = remove from that site only.</summary>
[Index(nameof(SiteId), nameof(NormalizedPhone), IsUnique = true)]
public partial class Customer
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int SiteId { get; set; }

    /// <summary>Digits-only phone for uniqueness and lookup within the site.</summary>
    [StringLength(50)]
    public string NormalizedPhone { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? City { get; set; }

    [StringLength(500)]
    public string? DefaultAddress { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool MarketingApproval { get; set; }

    public bool MarketingEmail { get; set; }

    public bool MarketingSms { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    [ForeignKey("AccountId")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;

    [InverseProperty("Customer")]
    public virtual ICollection<Order> Order { get; set; } = new List<Order>();
}
