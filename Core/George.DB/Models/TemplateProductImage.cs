using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("TemplateProductImage")]
public partial class TemplateProductImage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int TemplateProductId { get; set; }

    public int SortOrder { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    public int? MediaId { get; set; }

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductImages")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;

    [ForeignKey("MediaId")]
    [InverseProperty("TemplateProductImages")]
    public virtual Medium? Media { get; set; }
}
