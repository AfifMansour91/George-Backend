using George.DB;
using George.Services.Response;

namespace George.Services;

public static class DashboardMetricsHelper
{
    public static decimal? PctChange(decimal current, decimal baseline)
    {
        if (baseline == 0m)
            return current == 0m ? 0m : null;
        return Math.Round((current - baseline) / baseline * 100m, 1, MidpointRounding.AwayFromZero);
    }

    public static DashboardKpiTrendDto BuildTrend(
        decimal income, int orders, decimal avgOrder, decimal avgItems,
        decimal baseIncome, int baseOrders, decimal baseAvgOrder, decimal baseAvgItems)
    {
        return new DashboardKpiTrendDto
        {
            IncomePct = PctChange(income, baseIncome),
            OrdersPct = PctChange(orders, baseOrders),
            AvgOrderPct = PctChange(avgOrder, baseAvgOrder),
            AvgItemsPct = PctChange(avgItems, baseAvgItems),
        };
    }

    public static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

    public static bool IsUnsettledPayment(string? status)
    {
        var s = (status ?? "").Trim();
        return !string.Equals(s, "Paid", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(s, "Captured", StringComparison.OrdinalIgnoreCase);
    }

    public static DateTime? OrderScheduledDate(Order o)
    {
        var isPickup = string.Equals(o.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase);
        var dt = isPickup ? o.PickupDate : o.DeliveryDate;
        if (dt == null) return null;
        return dt.Value.Date;
    }
}
