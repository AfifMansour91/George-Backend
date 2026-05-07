using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductId", "GlobalBrandId")]
[Index("GlobalBrandId", Name = "IX_TemplateProductBrand_GlobalBrandId")]
public partial class TemplateProductBrand
{
    [Key]
    public int TemplateProductId { get; set; }

    [Key]
    public int GlobalBrandId { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("GlobalBrandId")]
    [InverseProperty("TemplateProductBrand")]
    public virtual GlobalBrand GlobalBrand { get; set; } = null!;

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductBrand")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
