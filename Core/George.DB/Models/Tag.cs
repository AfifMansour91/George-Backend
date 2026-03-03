using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Tag_AccountId")]
public partial class Tag
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
    [InverseProperty("Tag")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("TagCreationUser")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("TagUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("TagId")]
    [InverseProperty("Tag")]
    public virtual ICollection<Media> Media { get; set; } = new List<Media>();

    [ForeignKey("TagId")]
    [InverseProperty("Tag")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [ForeignKey("TagId")]
    [InverseProperty("Tag")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();
}
