using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("SyncJobLog")]
public partial class SyncJobLog
{
    [Key]
    public long Id { get; set; }

    public long SyncJobId { get; set; }

    [StringLength(20)]
    public string Level { get; set; } = null!;

    [StringLength(2000)]
    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    [ForeignKey("SyncJobId")]
    [InverseProperty("SyncJobLogs")]
    public virtual SyncJob SyncJob { get; set; } = null!;
}
