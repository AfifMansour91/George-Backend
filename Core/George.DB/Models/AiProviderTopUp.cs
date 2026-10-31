using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// The super-admin's note of the balance shown in a provider's console (right after loading money, or whenever they check).
/// The newest row per provider is the anchor of the wallet estimate: remaining = BalanceUsd - <see cref="AiProviderSpend"/>
/// since CreationTime. AiStudio_Phase3_Install.sql §7.
/// </summary>
[Index("Provider", "Id", Name = "IX_AiProviderTopUp_Provider_Id")]
public partial class AiProviderTopUp
{
    [Key]
    public int Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [StringLength(20)]
    public string Provider { get; set; } = null!;

    /// <summary>The balance in the console at that moment - not the amount loaded.</summary>
    [Precision(12, 2)]
    public decimal BalanceUsd { get; set; }

    /// <summary>Warn on the admin page when the estimate drops under this; NULL = the default (20).</summary>
    [Precision(12, 2)]
    public decimal? AlertBelowUsd { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    public int? CreationUserId { get; set; }
}
