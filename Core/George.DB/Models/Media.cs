using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Media_AccountId")]
public partial class Media
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

    public int? AccountId { get; set; }

    public bool IsGlobal { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Media")]
    public virtual Account? Account { get; set; }

    [InverseProperty("Media")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("Media")]
    public virtual BusinessType? BusinessType { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("MediaCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("HomeVideoMedia")]
    public virtual ICollection<KioskSettings> KioskSettings { get; set; } = new List<KioskSettings>();

    [InverseProperty("Media")]
    public virtual ICollection<KioskSettingsHomeImage> KioskSettingsHomeImage { get; set; } = new List<KioskSettingsHomeImage>();

    [InverseProperty("Media")]
    public virtual ICollection<ProductImage> ProductImage { get; set; } = new List<ProductImage>();

    [InverseProperty("Media")]
    public virtual ICollection<TemplateProductImage> TemplateProductImage { get; set; } = new List<TemplateProductImage>();

    [ForeignKey("TypeId")]
    [InverseProperty("Media")]
    public virtual MediaType? Type { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("MediaUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Category> Category { get; set; } = new List<Category>();

    [ForeignKey("MediaId")]
    [InverseProperty("Media")]
    public virtual ICollection<Tag> Tag { get; set; } = new List<Tag>();
}
