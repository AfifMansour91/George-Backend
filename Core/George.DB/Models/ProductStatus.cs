using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__ProductS__737584F618843EA7", IsUnique = true)]
public partial class ProductStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("Status")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [InverseProperty("Status")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();
}
