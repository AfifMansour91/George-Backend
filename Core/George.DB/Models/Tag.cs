using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Tag")]
[Index("Name", Name = "UQ__Tag__737584F686AD379B", IsUnique = true)]
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

    [ForeignKey("CreationUserId")]
    [InverseProperty("TagCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("TagUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
