using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Supplier_AccountId")]
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

    public int? AccountId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Supplier")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("SupplierCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Supplier")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [InverseProperty("Supplier")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("SupplierUpdateUser")]
    public virtual User? UpdateUser { get; set; }
}
