using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("SyncJob")]
public partial class SyncJob
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    [StringLength(50)]
    public string JobType { get; set; } = null!;

    [StringLength(30)]
    public string Status { get; set; } = null!;

    public int? RequestedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [StringLength(2000)]
    public string? Message { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("SyncJobs")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("RequestedBy")]
    [InverseProperty("SyncJobs")]
    public virtual User? RequestedByNavigation { get; set; }

    [InverseProperty("SyncJob")]
    public virtual ICollection<SyncJobLog> SyncJobLogs { get; set; } = new List<SyncJobLog>();
}
