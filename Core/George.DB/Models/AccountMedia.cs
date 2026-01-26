using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Records that an account "uses" a media file. Enables multiple accounts to use the same media (e.g. global media).
/// </summary>
[PrimaryKey("AccountId", "MediaId")]
[Table("AccountMedia")]
[Index("AccountId", Name = "IX_AccountMedia_AccountId")]
[Index("MediaId", Name = "IX_AccountMedia_MediaId")]
public partial class AccountMedia
{
    [Key]
    public int AccountId { get; set; }

    [Key]
    public int MediaId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountMedia")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("MediaId")]
    [InverseProperty("AccountMedia")]
    public virtual Medium Media { get; set; } = null!;
}
