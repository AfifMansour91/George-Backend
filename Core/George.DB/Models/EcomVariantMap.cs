using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("EcomVariantMap")]
public partial class EcomVariantMap
{
    [Key]
    public long Id { get; set; }

    public long AccountProductVariantId { get; set; }

    [StringLength(100)]
    public string RemoteVariantId { get; set; } = null!;

    public DateTime SyncedAt { get; set; }

    [ForeignKey("AccountProductVariantId")]
    [InverseProperty("EcomVariantMaps")]
    public virtual AccountProductVariant AccountProductVariant { get; set; } = null!;
}
