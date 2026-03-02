using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Brand_AccountId")]
public partial class Brand
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
    [InverseProperty("Brand")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("BrandCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Brand")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [InverseProperty("Brand")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("BrandUpdateUser")]
    public virtual User? UpdateUser { get; set; }
}
