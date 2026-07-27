using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>CRM: Customer per site. One record per (SiteId, NormalizedPhone). Same person at another site = separate row. Delete = remove from that site only.
/// The unique index is filtered in SQL (WHERE NormalizedPhone &lt;&gt; '') so multiple phone-less customers can coexist per site — the attribute below can't express the filter.</summary>
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

    /// <summary>Street and building number (mirrors <see cref="Order.DeliveryStreet"/>).</summary>
    [StringLength(400)]
    public string? DeliveryStreet { get; set; }

    [StringLength(64)]
    public string? DeliveryApartment { get; set; }

    [StringLength(32)]
    public string? DeliveryFloor { get; set; }

    [StringLength(64)]
    public string? DeliveryEntranceCode { get; set; }

    [StringLength(500)]
    public string? DefaultAddress { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool MarketingApproval { get; set; }

    public bool MarketingEmail { get; set; }

    public bool MarketingSms { get; set; }

    /// <summary>Phone order: default manual discount type (<c>percent</c> | <c>amount</c>) for this customer at this site.</summary>
    [StringLength(20)]
    public string? PermanentDiscountType { get; set; }

    /// <summary>Phone order: default discount percent or fixed NIS amount.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? PermanentDiscountValue { get; set; }

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

    [InverseProperty("Customer")]
    public virtual ICollection<CustomerPaymentMethod> CustomerPaymentMethod { get; set; } = new List<CustomerPaymentMethod>();
}
