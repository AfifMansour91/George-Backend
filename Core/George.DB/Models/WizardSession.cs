using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("WizardSession")]
public partial class WizardSession
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int StartedByUserId { get; set; }

    public int Step { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [StringLength(20)]
    public string ContentOwner { get; set; } = null!;

    [StringLength(100)]
    public string? InviteToken { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("WizardSessions")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("StartedByUserId")]
    [InverseProperty("WizardSessions")]
    public virtual User StartedByUser { get; set; } = null!;
}
