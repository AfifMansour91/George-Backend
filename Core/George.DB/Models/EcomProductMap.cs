using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("EcomProductMap")]
public partial class EcomProductMap
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    [StringLength(100)]
    public string RemoteProductId { get; set; } = null!;

    public DateTime SyncedAt { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("EcomProductMaps")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;
}
