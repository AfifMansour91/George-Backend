using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("AccountId", "SiteId", "MediaId")]
[Index("AccountId", Name = "IX_AccountMedia_AccountId")]
[Index("AccountId", "SiteId", Name = "IX_AccountMedia_AccountId_SiteId")]
[Index("MediaId", Name = "IX_AccountMedia_MediaId")]
[Index("AccountId", "MediaId", Name = "UQ_AccountMedia_AccountId_MediaId", IsUnique = true)]
public partial class AccountMedia
{
    public int Id { get; set; }

    [Key]
    public int AccountId { get; set; }

    [Key]
    public int MediaId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int? CreationUserId { get; set; }

    [Key]
    public int SiteId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountMedia")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("CreationUserId")]
    [InverseProperty("AccountMedia")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("MediaId")]
    [InverseProperty("AccountMedia")]
    public virtual Media Media { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountMedia")]
    public virtual Site Site { get; set; } = null!;
}
