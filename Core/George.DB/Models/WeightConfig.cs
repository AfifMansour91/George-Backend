using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("WeightConfig")]
public partial class WeightConfig
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public int? UnitId { get; set; }

    [StringLength(50)]
    public string? StartWeight { get; set; }

    [StringLength(50)]
    public string? Step { get; set; }

    public bool? FixedWeightPerUnit { get; set; }

    [StringLength(50)]
    public string? UnitWeight { get; set; }

    public int? UnitWeightModeId { get; set; }

    [StringLength(2000)]
    public string? WeightOptions { get; set; }

    public bool? WeightByVariant { get; set; }

    public bool? ShowPricePer100g { get; set; }

    [InverseProperty("WeightConfig")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("WeightConfig")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UnitId")]
    [InverseProperty("WeightConfigs")]
    public virtual Unit? Unit { get; set; }

    [ForeignKey("UnitWeightModeId")]
    [InverseProperty("WeightConfigs")]
    public virtual UnitWeightMode? UnitWeightMode { get; set; }
}
