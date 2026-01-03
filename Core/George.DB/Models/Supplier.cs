using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Supplier")]
[Index("Name", Name = "UQ__Supplier__737584F6117D9B1A", IsUnique = true)]
public partial class Supplier
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [ForeignKey("CreationUserId")]
    [InverseProperty("SupplierCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Supplier")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("Supplier")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("SupplierUpdateUsers")]
    public virtual User? UpdateUser { get; set; }
}
