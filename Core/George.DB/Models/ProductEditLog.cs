using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductEditLog")]
public partial class ProductEditLog
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    public int UserId { get; set; }

    [StringLength(30)]
    public string FromStatus { get; set; } = null!;

    [StringLength(30)]
    public string ToStatus { get; set; } = null!;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("ProductEditLogs")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ProductEditLogs")]
    public virtual User User { get; set; } = null!;
}
