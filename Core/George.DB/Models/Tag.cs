using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Tag")]
[Index("Name", Name = "IX_Tag_Name", IsUnique = true)]
public partial class Tag
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(120)]
    public string? Slug { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty("Tag")]
    public virtual ICollection<AccountProductTag> AccountProductTags { get; set; } = new List<AccountProductTag>();

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<ProductTemplate> ProductTemplates { get; set; } = new List<ProductTemplate>();
}
