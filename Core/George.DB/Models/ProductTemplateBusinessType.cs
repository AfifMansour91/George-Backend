using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductTemplateId", "BusinessTypeId")]
[Table("ProductTemplateBusinessType")]
public partial class ProductTemplateBusinessType
{
    [Key]
    public long ProductTemplateId { get; set; }

    [Key]
    public int BusinessTypeId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("ProductTemplateBusinessTypes")]
    public virtual BusinessType BusinessType { get; set; } = null!;

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplateBusinessTypes")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;
}
