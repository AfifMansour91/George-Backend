using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("SiteId", Name = "IX_SiteOrderReceptionClosed_SiteId")]
[Index("SiteId", "ClosedDate", "Type", Name = "UX_SiteOrderReceptionClosed_SiteId_ClosedDate_Type", IsUnique = true)]
public partial class SiteOrderReceptionClosed
{
    [Key]
    public int Id { get; set; }

    public int SiteId { get; set; }

    public DateOnly ClosedDate { get; set; }

    [StringLength(20)]
    public string Type { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("SiteOrderReceptionClosed")]
    public virtual Site Site { get; set; } = null!;
}
