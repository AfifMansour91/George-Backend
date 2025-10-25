using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductVariant")]
public partial class AccountProductVariant
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    [StringLength(100)]
    public string? VariantSku { get; set; }

    [StringLength(300)]
    public string? VariantTitle { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Price { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? StockQuantity { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? WeightGrams { get; set; }

    public bool IsEnabled { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductVariants")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;

    [InverseProperty("AccountProductVariant")]
    public virtual ICollection<AccountProductVariantOption> AccountProductVariantOptions { get; set; } = new List<AccountProductVariantOption>();

    [InverseProperty("AccountProductVariant")]
    public virtual ICollection<EcomVariantMap> EcomVariantMaps { get; set; } = new List<EcomVariantMap>();
}
