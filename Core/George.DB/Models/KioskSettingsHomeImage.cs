using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_KioskSettingsHomeImage_AccountId")]
[Index("MediaId", Name = "IX_KioskSettingsHomeImage_MediaId")]
public partial class KioskSettingsHomeImage
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int MediaId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("KioskSettingsHomeImage")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("MediaId")]
    [InverseProperty("KioskSettingsHomeImage")]
    public virtual Media Media { get; set; } = null!;
}
