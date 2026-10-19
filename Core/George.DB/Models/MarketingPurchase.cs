using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One self-service purchase of a <see cref="MarketingPackage"/>, paid on the PLATFORM's Cardcom terminal (Giorgio's,
/// never the shop's). Lifecycle: initiated (hosted page created) → paid | failed | canceled. A paid row owns exactly
/// one <see cref="MarketingQuotaLedger"/> purchase entry (<see cref="LedgerEntryId"/>), so a webhook and the return
/// page racing on the same payment can never credit twice.
/// </summary>
[Index("AccountId", "Id", Name = "IX_MarketingPurchase_AccountId_Id", IsDescending = new[] { false, true })]
[Index("LowProfileId", Name = "UQ_MarketingPurchase_LowProfileId", IsUnique = true)]
public partial class MarketingPurchase
{
    [Key]
    public int Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int AccountId { get; set; }

    public int? PackageId { get; set; }

    /// <summary>Snapshot of the package at purchase time - the catalog may change later.</summary>
    [StringLength(100)]
    public string? PackageName { get; set; }

    public int Messages { get; set; }

    /// <summary>Package price before VAT, as shown in the catalog.</summary>
    [Precision(18, 2)]
    public decimal Price { get; set; }

    [Precision(5, 2)]
    public decimal VatPercent { get; set; }

    /// <summary>The amount actually charged (price + VAT).</summary>
    [Precision(18, 2)]
    public decimal Total { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "ILS";

    /// <summary><c>initiated</c> | <c>paid</c> | <c>failed</c> | <c>canceled</c>.</summary>
    [StringLength(20)]
    public string Status { get; set; } = "initiated";

    [StringLength(20)]
    public string Gateway { get; set; } = "cardcom";

    /// <summary>Cardcom hosted-page session id - the key both the webhook and the return page come back with.</summary>
    [StringLength(100)]
    public string? LowProfileId { get; set; }

    [StringLength(100)]
    public string? TransactionId { get; set; }

    [StringLength(50)]
    public string? ApprovalNumber { get; set; }

    [StringLength(4)]
    public string? CardLast4 { get; set; }

    [StringLength(50)]
    public string? CardBrand { get; set; }

    [StringLength(50)]
    public string? DocumentNumber { get; set; }

    [StringLength(1000)]
    public string? DocumentUrl { get; set; }

    [Precision(0)]
    public DateTime? PaidAt { get; set; }

    public long? LedgerEntryId { get; set; }

    /// <summary>The money was taken but the SMS provider (Inforu sub-account) was not topped up - retried by the next confirm / super-admin.</summary>
    [StringLength(500)]
    public string? ProviderSyncError { get; set; }

    /// <summary>Gateway's reason when the payment failed, or the last internal error.</summary>
    [StringLength(500)]
    public string? ErrorDescription { get; set; }

    public int? CreationUserId { get; set; }
}
