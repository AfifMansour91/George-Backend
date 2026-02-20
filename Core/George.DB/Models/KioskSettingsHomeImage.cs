using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("KioskSettingsHomeImage")]
[Index(nameof(AccountId), Name = "IX_KioskSettingsHomeImage_AccountId")]
[Index(nameof(MediaId), Name = "IX_KioskSettingsHomeImage_MediaId")]
public partial class KioskSettingsHomeImage
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int MediaId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey(nameof(AccountId))]
    [InverseProperty("KioskSettingsHomeImages")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey(nameof(MediaId))]
    [InverseProperty("KioskSettingsHomeImages")]
    public virtual Medium Media { get; set; } = null!;
}
