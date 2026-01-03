using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductStatus")]
[Index("Name", Name = "UQ__ProductS__737584F6968B565D", IsUnique = true)]
public partial class ProductStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("Status")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("Status")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
