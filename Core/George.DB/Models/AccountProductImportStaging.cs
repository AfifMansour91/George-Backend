using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductImportStaging")]
public partial class AccountProductImportStaging
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    [StringLength(255)]
    public string? SourceFileName { get; set; }

    public int RowNumber { get; set; }

    [StringLength(300)]
    public string? RawName { get; set; }

    public string? RawDescription { get; set; }

    [StringLength(100)]
    public string? RawSku { get; set; }

    [StringLength(1000)]
    public string? RawImageUrl { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? RawPrice { get; set; }

    [StringLength(200)]
    public string? RawWeightInfo { get; set; }

    public long? MatchedProductTemplateId { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? MatchConfidence { get; set; }

    public bool UseClientImage { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = null!;

    [ForeignKey("AccountId")]
    [InverseProperty("AccountProductImportStagings")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("MatchedProductTemplateId")]
    [InverseProperty("AccountProductImportStagings")]
    public virtual ProductTemplate? MatchedProductTemplate { get; set; }
}
