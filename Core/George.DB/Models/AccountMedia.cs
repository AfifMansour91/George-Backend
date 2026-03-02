using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Records that an account/site "uses" a media file. Media is scoped per site so multiple sites under one account do not see each other's media.
/// </summary>
[PrimaryKey("AccountId", "SiteId", "MediaId")]
[Table("AccountMedia")]
[Index("AccountId", Name = "IX_AccountMedia_AccountId")]
[Index("AccountId", "SiteId", Name = "IX_AccountMedia_AccountId_SiteId")]
[Index("MediaId", Name = "IX_AccountMedia_MediaId")]
public partial class AccountMedia
{
    [Key]
    public int AccountId { get; set; }

    [Key]
    public int SiteId { get; set; }

    [Key]
    public int MediaId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountMedia")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountMedia")]
    public virtual Site Site { get; set; } = null!;  // Required: media is scoped per site

    [ForeignKey("MediaId")]
    [InverseProperty("AccountMedia")]
    public virtual Medium Media { get; set; } = null!;
}
