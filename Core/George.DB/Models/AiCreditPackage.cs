using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// An AI Studio credit package a shop can buy by itself (pattern: <see cref="MarketingPackage"/>). The catalog is global - the
/// super-admin edits it, every paid-mode shop sees the active rows. Prices exclude VAT; the charged total is computed at
/// purchase time from <c>Billing:VatPercent</c>. AiStudio_Phase3_Install.sql.
/// </summary>
public partial class AiCreditPackage
{
    [Key]
    public int Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    /// <summary>Shown to the shop, e.g. "300 קרדיטים". Empty = the UI formats the credit count.</summary>
    [StringLength(100)]
    public string? Name { get; set; }

    public int Credits { get; set; }

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
