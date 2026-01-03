using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ShippingClass")]
[Index("Name", Name = "UQ__Shipping__737584F6FFB47F5F", IsUnique = true)]
public partial class ShippingClass
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("ShippingClass")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("ShippingClass")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
