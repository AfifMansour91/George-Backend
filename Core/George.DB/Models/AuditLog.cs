using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AuditLog")]
public partial class AuditLog
{
    [Key]
    public long Id { get; set; }

    public int? UserId { get; set; }

    [StringLength(200)]
    public string Action { get; set; } = null!;

    [StringLength(100)]
    public string EntityName { get; set; } = null!;

    public long? EntityId { get; set; }

    public string? Payload { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("AuditLogs")]
    public virtual User? User { get; set; }
}
