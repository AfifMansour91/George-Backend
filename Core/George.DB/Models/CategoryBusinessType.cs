using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("CategoryId", "BusinessTypeId")]
[Table("CategoryBusinessType")]
public partial class CategoryBusinessType
{
    [Key]
    public int CategoryId { get; set; }

    [Key]
    public int BusinessTypeId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("CategoryBusinessTypes")]
    public virtual BusinessType BusinessType { get; set; } = null!;

    [ForeignKey("CategoryId")]
    [InverseProperty("CategoryBusinessTypes")]
    public virtual Category Category { get; set; } = null!;
}
