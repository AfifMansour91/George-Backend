using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("BusinessType")]
public partial class BusinessType
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [InverseProperty("BusinessType")]
    public virtual ICollection<AccountBusinessType> AccountBusinessTypes { get; set; } = new List<AccountBusinessType>();

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("BusinessTypes")]
    public virtual ICollection<ProductTemplate> ProductTemplates { get; set; } = new List<ProductTemplate>();
}
