using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// A marketing-SMS package a shop can buy by itself (מסלולי Giorgio, "SMS שיווקי: חבילות לבחירה"). The catalog is
/// global - the super-admin edits it, every shop sees the active rows. Prices exclude VAT; the charged total is
/// computed at purchase time from <c>Billing:VatPercent</c>.
/// </summary>
public partial class MarketingPackage
{
    [Key]
    public int Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    /// <summary>Shown to the shop, e.g. "2,500 הודעות". Empty = the UI formats the message count.</summary>
    [StringLength(100)]
    public string? Name { get; set; }

    public int Messages { get; set; }

    /// <summary>Price in ILS before VAT.</summary>
    [Precision(18, 2)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>Optional one-liner under the price ("הכי משתלם").</summary>
    [StringLength(200)]
    public string? Badge { get; set; }

    public int? UpdateUserId { get; set; }
}
