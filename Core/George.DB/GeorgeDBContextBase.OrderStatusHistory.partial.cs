using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class GeorgeDBContextBase
{
    public virtual DbSet<OrderStatusHistory> OrderStatusHistory { get; set; } = null!;
}
