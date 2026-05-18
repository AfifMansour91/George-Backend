using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index(nameof(OrderId), nameof(CreationTime))]
public partial class OrderPaymentEvent
{
    [Key]
    public long Id { get; set; }

    public int OrderId { get; set; }

    [StringLength(64)]
    public string EventType { get; set; } = null!;

    [StringLength(32)]
    public string Provider { get; set; } = null!;

    [StringLength(32)]
    public string? StatusCode { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(64)]
    public string? GatewayTransactionId { get; set; }

    [StringLength(32)]
    public string? MaskedToken { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Amount { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? RawResponseJson { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [ForeignKey(nameof(OrderId))]
    public virtual Order Order { get; set; } = null!;
}
