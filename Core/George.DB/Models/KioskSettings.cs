using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("KioskSettings")]
public partial class KioskSettings
{
    [Key]
    [ForeignKey(nameof(Account))]
    public int AccountId { get; set; }

    [StringLength(1000)]
    public string? KioskLogoUrl { get; set; }

    [StringLength(50)]
    public string? HeaderBgColor { get; set; }

    [StringLength(20)]
    public string? HomeBgType { get; set; }

    /// <summary>FK to Media table for home page background video.</summary>
    public int? HomeVideoMediaId { get; set; }

    public int? HomeImageIntervalSeconds { get; set; }

    [ForeignKey(nameof(HomeVideoMediaId))]
    [InverseProperty("KioskSettingsHomeVideos")]
    public virtual Medium? HomeVideoMedia { get; set; }

    [StringLength(50)]
    public string? PrimaryColor { get; set; }

    [StringLength(50)]
    public string? SecondaryColor { get; set; }

    [StringLength(500)]
    public string? PosProductsTitle { get; set; }

    /// <summary>POS products step: "upsells" (related/complementary only), "category", or "combined".</summary>
    [StringLength(20)]
    public string? PosProductsType { get; set; }

    /// <summary>When PosProductsType is "category" or "combined", the category to show products from.</summary>
    public int? PosProductsCategoryId { get; set; }

    public bool CreditEnabled { get; set; }

    public bool CashAtRegisterEnabled { get; set; }

    public virtual Account Account { get; set; } = null!;
}
