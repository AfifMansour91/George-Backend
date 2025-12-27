using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("MediaId", Name = "IX_ProductTemplateMedia_MediaId")]
public partial class ProductTemplateMedium
{
    [Key]
    public long Id { get; set; }

    public long ProductTemplateId { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [StringLength(300)]
    public string? AltText { get; set; }

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public long? MediaId { get; set; }

    [ForeignKey("MediaId")]
    [InverseProperty("ProductTemplateMedia")]
    public virtual Medium? Media { get; set; }

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplateMedia")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;
}
