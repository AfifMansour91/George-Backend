using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductTemplateSelectableWeight")]
public partial class ProductTemplateSelectableWeight
{
    [Key]
    public long Id { get; set; }

    public long ProductTemplateId { get; set; }

    [StringLength(100)]
    public string Label { get; set; } = null!;

    [Column(TypeName = "decimal(18, 3)")]
    public decimal WeightGrams { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplateSelectableWeights")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;
}
