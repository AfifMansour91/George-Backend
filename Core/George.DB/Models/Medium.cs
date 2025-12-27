using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("BusinessTypeId", Name = "IX_Media_BusinessTypeId")]
[Index("Name", Name = "IX_Media_Name")]
[Index("Type", Name = "IX_Media_Type")]
public partial class Medium
{
    [Key]
    public long Id { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [StringLength(255)]
    public string Name { get; set; } = null!;

    [StringLength(20)]
    public string? Type { get; set; }

    public int? BusinessTypeId { get; set; }

    public long? FileSize { get; set; }

    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    [InverseProperty("Media")]
    public virtual ICollection<AccountProductMedium> AccountProductMedia { get; set; } = new List<AccountProductMedium>();

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("Media")]
    public virtual BusinessType? BusinessType { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("Media")]
    public virtual User? CreatedByUser { get; set; }

    [InverseProperty("Media")]
    public virtual ICollection<ProductTemplateMedium> ProductTemplateMedia { get; set; } = new List<ProductTemplateMedium>();

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
