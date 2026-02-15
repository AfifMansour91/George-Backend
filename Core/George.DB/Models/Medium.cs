using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class Medium
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

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [StringLength(300)]
    public string Name { get; set; } = null!;

    public int? TypeId { get; set; }

    public int? BusinessTypeId { get; set; }

    public long? FileSize { get; set; }

    public int? UsageCount { get; set; }

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("Media")]
    public virtual BusinessType? BusinessType { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("MediumCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("TypeId")]
    [InverseProperty("Media")]
    public virtual MediaType? Type { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("MediumUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("Media")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

    [InverseProperty("Media")]
    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    [InverseProperty("Media")]
    public virtual ICollection<TemplateProductImage> TemplateProductImages { get; set; } = new List<TemplateProductImage>();
}
