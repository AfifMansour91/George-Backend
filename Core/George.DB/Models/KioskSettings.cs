using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class KioskSettings
{
    [Key]
    public int AccountId { get; set; }

    [StringLength(1000)]
    public string? KioskLogoUrl { get; set; }

    [StringLength(50)]
    public string? HeaderBgColor { get; set; }

    [StringLength(20)]
    public string? HomeBgType { get; set; }

    public int? HomeImageIntervalSeconds { get; set; }

    [StringLength(50)]
    public string? PrimaryColor { get; set; }

    [StringLength(50)]
    public string? SecondaryColor { get; set; }

    [StringLength(500)]
    public string? PosProductsTitle { get; set; }

    public bool CreditEnabled { get; set; }

    public bool CashAtRegisterEnabled { get; set; }

    /// <summary>Show "הזמנה חוזרת" (Repeat Order) button in kiosk (default false).</summary>
    public bool ShowDuplicateOrderButton { get; set; } = false;

    public int? HomeVideoMediaId { get; set; }

    [StringLength(20)]
    public string? PosProductsType { get; set; }

    public int? PosProductsCategoryId { get; set; }

    /// <summary>Enable POS products (upsell) step (default true).</summary>
    public bool PosProductsEnabled { get; set; } = true;

    [StringLength(200)]
    public string? ButtonTextToPaymentOrViewOrder { get; set; }

    [StringLength(200)]
    public string? ButtonTextCartToPayment { get; set; }

    [StringLength(200)]
    public string? ButtonTextUpsellContinueToPayment { get; set; }

    /// <summary>Seconds before "Are you still there?" popup (default 60).</summary>
    public int? InactivityPopupSeconds { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("KioskSettings")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("HomeVideoMediaId")]
    [InverseProperty("KioskSettings")]
    public virtual Media? HomeVideoMedia { get; set; }
}
