using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class DashboardStorage : StorageBase
{
    private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();
    private static readonly string[] ActiveStatuses = ["New", "InTreatment", "Ready"];
    private static readonly string[] ClosedStatuses = ["Completed", "Cancelled", "Delivered"];

    public DashboardStorage(GeorgeDBContext dbContext, ILogger<DashboardStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<List<Order>> GetActivePipelineOrdersAsync(
        IReadOnlyList<int> siteIds,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<Order>();
        return await _dbContext.Order
            .AsNoTracking()
            .Include(o => o.Site)
            .Where(o => !o.IsDeleted
                        && siteIds.Contains(o.SiteId)
                        && ActiveStatuses.Contains(o.Status))
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>Non-cancelled orders whose <see cref="Order.CreationTime"/> falls in [fromUtc, toUtcExclusive).</summary>
    public async Task<List<Order>> GetOrdersCreatedInWindowAsync(
        int siteId,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken cancelToken)
    {
        var list = await _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted
                        && o.SiteId == siteId
                        && o.Status != "Cancelled"
                        && o.CreationTime >= fromUtc
                        && o.CreationTime < toUtcExclusive)
            .Include(o => o.OrderItem)
            .OrderByDescending(o => o.CreationTime)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        foreach (var o in list)
        {
            if (o.OrderItem == null) continue;
            o.OrderItem = o.OrderItem.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ToList();
        }

        return list;
    }

    public async Task<List<Order>> GetForwardPipelineOrdersAsync(
        IReadOnlyList<int> siteIds,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<Order>();
        return await _dbContext.Order
            .AsNoTracking()
            .Include(o => o.Site)
            .Include(o => o.OrderItem)
            .Where(o => !o.IsDeleted
                        && siteIds.Contains(o.SiteId)
                        && !ClosedStatuses.Contains(o.Status)
                        && o.PaymentStatus != null
                        && o.PaymentStatus != "Paid"
                        && o.PaymentStatus != "Captured")
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    public async Task<List<Order>> GetOpenOrdersForDeliveryWindowAsync(
        IReadOnlyList<int> siteIds,
        DateTime scheduledFrom,
        DateTime scheduledToExclusive,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<Order>();
        var from = scheduledFrom.Date;
        var toEx = scheduledToExclusive.Date;

        var orders = await _dbContext.Order
            .AsNoTracking()
            .Include(o => o.OrderItem)
            .Where(o => !o.IsDeleted
                        && siteIds.Contains(o.SiteId)
                        && !ClosedStatuses.Contains(o.Status))
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        return orders.Where(o =>
        {
            var sched = GetScheduledDate(o);
            return sched.HasValue && sched.Value >= from && sched.Value < toEx;
        }).ToList();
    }

    public async Task<List<(OrderStatusHistory History, Order Order, Site? Site)>> GetRecentStatusEventsAsync(
        IReadOnlyList<int> siteIds,
        DateTime sinceUtc,
        int limit,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<(OrderStatusHistory, Order, Site?)>();

        var rows = await (
            from h in _dbContext.OrderStatusHistory.AsNoTracking()
            join o in _dbContext.Order.AsNoTracking() on h.OrderId equals o.Id
            join s in _dbContext.Site.AsNoTracking() on o.SiteId equals s.Id into siteJoin
            from s in siteJoin.DefaultIfEmpty()
            where !o.IsDeleted
                  && siteIds.Contains(o.SiteId)
                  && h.OccurredAt >= sinceUtc
            orderby h.OccurredAt descending
            select new { h, o, s }
        ).Take(limit * 2).ToListAsync(cancelToken).ConfigureAwait(false);

        return rows.Select(r => (r.h, r.o, (Site?)r.s)).ToList();
    }

    public async Task<List<(Order Order, Site? Site)>> GetRecentNewOrdersAsync(
        IReadOnlyList<int> siteIds,
        DateTime sinceUtc,
        int limit,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<(Order, Site?)>();

        var rows = await _dbContext.Order
            .AsNoTracking()
            .Include(o => o.Site)
            .Where(o => !o.IsDeleted
                        && siteIds.Contains(o.SiteId)
                        && o.CreationTime >= sinceUtc)
            .OrderByDescending(o => o.CreationTime)
            .Take(limit)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        return rows.Select(o => (o, o.Site)).ToList();
    }

    public async Task<List<(OrderPaymentEvent Event, Order Order, Site? Site)>> GetRecentPaymentEventsAsync(
        IReadOnlyList<int> siteIds,
        DateTime sinceUtc,
        int limit,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<(OrderPaymentEvent, Order, Site?)>();

        var rows = await (
            from e in _dbContext.OrderPaymentEvent.AsNoTracking()
            join o in _dbContext.Order.AsNoTracking() on e.OrderId equals o.Id
            join s in _dbContext.Site.AsNoTracking() on o.SiteId equals s.Id into siteJoin
            from s in siteJoin.DefaultIfEmpty()
            where !o.IsDeleted
                  && siteIds.Contains(o.SiteId)
                  && e.CreationTime >= sinceUtc
                  && (e.EventType == "CaptureAuthorization"
                      || e.EventType == "ChargeToken"
                      || e.EventType == "Refund"
                      || e.EventType == "TokenAuthorizationHold"
                      || e.EventType == "Void")
            orderby e.CreationTime descending
            select new { e, o, s }
        ).Take(limit).ToListAsync(cancelToken).ConfigureAwait(false);

        return rows.Select(r => (r.e, r.o, (Site?)r.s)).ToList();
    }

    public async Task<List<(Product Product, Site Site)>> GetRecentProductUpdatesAsync(
        IReadOnlyList<int> siteIds,
        DateTime sinceUtc,
        int limit,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0) return new List<(Product, Site)>();

        var products = await _dbContext.Product
            .AsNoTracking()
            .Include(p => p.Site)
            .Where(p => !p.IsDeleted
                        && p.UpdatedDate != null
                        && p.UpdatedDate >= sinceUtc
                        && p.Site.Any(s => siteIds.Contains(s.Id)))
            .OrderByDescending(p => p.UpdatedDate)
            .Take(limit)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        var result = new List<(Product, Site)>();
        foreach (var p in products)
        {
            var site = p.Site.FirstOrDefault(s => siteIds.Contains(s.Id));
            if (site != null)
                result.Add((p, site));
        }

        return result;
    }

    public async Task<decimal> GetWeekdayAverageOrdersAsync(
        IReadOnlyList<int> siteIds,
        DayOfWeek weekday,
        int lookbackWeeks,
        CancellationToken cancelToken)
    {
        if (siteIds.Count == 0 || lookbackWeeks <= 0) return 0m;

        var utcNow = DateTime.UtcNow;
        var fromUtc = utcNow.AddDays(-7 * lookbackWeeks);

        var orders = await _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted
                        && siteIds.Contains(o.SiteId)
                        && o.Status != "Cancelled"
                        && o.CreationTime >= fromUtc
                        && o.CreationTime < utcNow)
            .Select(o => o.CreationTime)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        var israelDates = orders
            .Select(d => ToIsraelCalendarDate(d))
            .ToList();

        var matchingDays = israelDates
            .Where(d => d.DayOfWeek == weekday)
            .Distinct()
            .Count();

        if (matchingDays == 0) return 0m;
        var countOnWeekday = israelDates.Count(d => d.DayOfWeek == weekday);
        return Math.Round((decimal)countOnWeekday / matchingDays, 1, MidpointRounding.AwayFromZero);
    }

    private static DateTime? GetScheduledDate(Order o)
    {
        var isPickup = string.Equals(o.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase);
        var dt = isPickup ? o.PickupDate : o.DeliveryDate;
        return dt?.Date;
    }

    private static TimeZoneInfo ResolveIsraelTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        }
    }

    private static DateTime AssumeUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static DateTime ToIsraelCalendarDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(AssumeUtc(utc), IsraelTimeZone).Date;
}
