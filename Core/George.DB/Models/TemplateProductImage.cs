using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("MediaId", Name = "IX_TemplateProductImage_MediaId")]
[Index("TemplateProductId", "Url", Name = "IX_TemplateProductImage_TemplateProductId_Url", IsUnique = true)]
public partial class TemplateProductImage
{
    public int TemplateProductId { get; set; }

    public int SortOrder { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    public int? MediaId { get; set; }

    [Key]
    public long Id { get; set; }

    [ForeignKey("MediaId")]
    [InverseProperty("TemplateProductImage")]
    public virtual Media? Media { get; set; }

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductImage")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
