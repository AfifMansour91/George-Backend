using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Sprint 2: Date when order reception is closed for a site (Delivery or Pickup). Used for פתיחה/סגירת קבלת הזמנות.</summary>
[Table("SiteOrderReceptionClosed")]
public partial class SiteOrderReceptionClosed
{
    [Key]
    public int Id { get; set; }

    public int SiteId { get; set; }

    /// <summary>Date only (no time) when reception is closed.</summary>
    [Column(TypeName = "date")]
    public DateTime ClosedDate { get; set; }

    /// <summary>Delivery | Pickup</summary>
    [StringLength(20)]
    public string Type { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("SiteOrderReceptionClosed")]
    public virtual Site Site { get; set; } = null!;
}
