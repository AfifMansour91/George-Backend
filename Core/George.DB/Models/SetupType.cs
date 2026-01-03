using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("SetupType")]
[Index("Name", Name = "UQ__SetupTyp__737584F63DF762F1", IsUnique = true)]
public partial class SetupType
{
    [Key]
    public int Id { get; set; }

    [StringLength(40)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("SetupType")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("SetupType")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
