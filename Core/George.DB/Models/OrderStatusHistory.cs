using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("OrderStatusHistory")]
[Index(nameof(OrderId), nameof(OccurredAt), Name = "IX_OrderStatusHistory_OrderId_OccurredAt")]
public partial class OrderStatusHistory
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime OccurredAt { get; set; }

    [ForeignKey(nameof(OrderId))]
    [InverseProperty(nameof(Order.OrderStatusHistory))]
    public virtual Order Order { get; set; } = null!;
}
