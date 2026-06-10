using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

public class DashboardService : ServiceBase
{
    private readonly DashboardStorage _dashboardStorage;
    private readonly IncomeReportStorage _incomeReportStorage;
    private readonly RevenueReportStorage _revenueReportStorage;
    private readonly SiteStorage _siteStorage;
    private readonly UserStorage _userStorage;
    private readonly OrderService _orderService;

    public DashboardService(
        ILogger<DashboardService> logger,
        IMapper mapper,
        CacheManager cache,
        DashboardStorage dashboardStorage,
        IncomeReportStorage incomeReportStorage,
        RevenueReportStorage revenueReportStorage,
        SiteStorage siteStorage,
        UserStorage userStorage,
        OrderService orderService)
        : base(logger, mapper, cache)
    {
        _dashboardStorage = dashboardStorage;
        _incomeReportStorage = incomeReportStorage;
        _revenueReportStorage = revenueReportStorage;
        _siteStorage = siteStorage;
        _userStorage = userStorage;
        _orderService = orderService;
    }

    public async Task<IApiResponse<DashboardSummaryRes>> GetSummaryAsync(
        int siteId,
        string period,
        DateTime? customFrom,
        DateTime? customTo,
        int? accountId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<DashboardSummaryRes>();
        var siteIds = await ResolveAccessibleSiteIdsAsync(siteId, accountId, cancelToken).ConfigureAwait(false);
        if (siteIds.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "No accessible sites.");

        var utcNow = DateTime.UtcNow;
        DateTime fromUtc;
        DateTime toUtcExclusive;
        try
        {
            (fromUtc, toUtcExclusive) = ReportPeriodRange.ResolveCurrentRange(period, customFrom, customTo, utcNow);
        }
        catch
        {
            return CreateResponse(response, StatusCode.InvalidRequest, "Invalid period or custom dates.");
        }

        var kpis = await BuildAggregatedKpisAsync(siteIds, fromUtc, toUtcExclusive, period, utcNow, cancelToken)
            .ConfigureAwait(false);
        var activeOrders = await BuildActiveOrdersAsync(siteIds, cancelToken).ConfigureAwait(false);
        var forwardOrders = await _dashboardStorage
            .GetForwardPipelineOrdersAsync(siteIds, cancelToken)
            .ConfigureAwait(false);
        var forwardProjection = BuildForwardProjection(forwardOrders, utcNow);
        var insights = await BuildInsightsAsync(siteIds, kpis, forwardProjection, fromUtc, toUtcExclusive, utcNow, cancelToken)
            .ConfigureAwait(false);

        response.Data = new DashboardSummaryRes
        {
            Kpis = kpis,
            ActiveOrders = activeOrders,
            ForwardProjection = forwardProjection,
            Insights = insights,
        };
        return response;
    }

    public async Task<IApiResponse<List<LiveActivityEventRes>>> GetLiveActivityAsync(
        int siteId,
        DateTime? since,
        int limit,
        int? accountId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<LiveActivityEventRes>> { Data = new List<LiveActivityEventRes>() };
        var siteIds = await ResolveAccessibleSiteIdsAsync(siteId, accountId, cancelToken).ConfigureAwait(false);
        if (siteIds.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "No accessible sites.");

        var sinceUtc = since ?? DateTime.UtcNow.AddHours(-24);
        var take = limit is > 0 and <= 100 ? limit : 20;
        var events = new List<LiveActivityEventRes>();

        var statusEvents = await _dashboardStorage
            .GetRecentStatusEventsAsync(siteIds, sinceUtc, take, cancelToken)
            .ConfigureAwait(false);
        foreach (var (history, order, site) in statusEvents)
        {
            var mapped = MapStatusHistoryEvent(history, order, site);
            if (mapped != null) events.Add(mapped);
        }

        var newOrders = await _dashboardStorage
            .GetRecentNewOrdersAsync(siteIds, sinceUtc, take, cancelToken)
            .ConfigureAwait(false);
        foreach (var (order, site) in newOrders)
            events.Add(MapNewOrderEvent(order, site));

        var productUpdates = await _dashboardStorage
            .GetRecentProductUpdatesAsync(siteIds, sinceUtc, take, cancelToken)
            .ConfigureAwait(false);
        foreach (var (product, site) in productUpdates)
            events.Add(MapProductUpdateEvent(product, site));

        var paymentEvents = await _dashboardStorage
            .GetRecentPaymentEventsAsync(siteIds, sinceUtc, take, cancelToken)
            .ConfigureAwait(false);
        foreach (var (paymentEvent, order, site) in paymentEvents)
        {
            var mapped = MapPaymentEvent(paymentEvent, order, site);
            if (mapped != null) events.Add(mapped);
        }

        response.Data = events
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .OrderByDescending(e => e.OccurredAt)
            .Take(take)
            .ToList();
        return response;
    }

    public async Task<IApiResponse<List<DeliveryProductRowRes>>> GetDeliveryProductsAsync(
        int siteId,
        string window,
        int limit,
        int? accountId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<DeliveryProductRowRes>> { Data = new List<DeliveryProductRowRes>() };
        var siteIds = await ResolveAccessibleSiteIdsAsync(siteId, accountId, cancelToken).ConfigureAwait(false);
        if (siteIds.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "No accessible sites.");

        var (from, toEx) = ResolveDeliveryWindow(window, DateTime.UtcNow);
        var orders = await _dashboardStorage
            .GetOpenOrdersForDeliveryWindowAsync(siteIds, from, toEx, cancelToken)
            .ConfigureAwait(false);

        var productIds = orders
            .SelectMany(o => o.OrderItem ?? Enumerable.Empty<OrderItem>())
            .Where(i => !i.IsDeleted && i.ProductId is > 0)
            .Select(i => i.ProductId!.Value)
            .Distinct()
            .ToList();
        var products = productIds.Count > 0
            ? await _incomeReportStorage.GetProductsWithCategoriesAsync(productIds, cancelToken).ConfigureAwait(false)
            : new Dictionary<int, Product>();

        var agg = new Dictionary<int, (string Name, decimal Qty, HashSet<int> OrderIds, bool Weighted)>();
        foreach (var order in orders)
        {
            foreach (var line in order.OrderItem ?? Enumerable.Empty<OrderItem>())
            {
                if (line.IsDeleted || line.ProductId is not > 0) continue;
                if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                var qty = line.PickedQuantity is > 0 ? line.PickedQuantity.Value : line.Quantity;
                if (!agg.ContainsKey(p.Id))
                    agg[p.Id] = (p.Name ?? "", 0m, new HashSet<int>(), p.IsWeighted == true);
                var t = agg[p.Id];
                t.Qty += qty;
                t.OrderIds.Add(order.Id);
                agg[p.Id] = t;
            }
        }

        var ranked = agg
            .OrderByDescending(kv => kv.Value.Qty)
            .Select(kv =>
            {
                var label = kv.Value.Weighted
                    ? $"{DashboardMetricsHelper.Round2(kv.Value.Qty)} ק\"ג"
                    : $"{DashboardMetricsHelper.Round2(kv.Value.Qty)} יח'";
                return new DeliveryProductRowRes
                {
                    ProductId = kv.Key,
                    Name = kv.Value.Name,
                    TotalQuantity = DashboardMetricsHelper.Round2(kv.Value.Qty),
                    QuantityLabel = label,
                    OrderCount = kv.Value.OrderIds.Count,
                };
            });

        response.Data = limit > 0 ? ranked.Take(limit).ToList() : ranked.ToList();
        return response;
    }

    private async Task<List<int>> ResolveAccessibleSiteIdsAsync(
        int siteId,
        int? accountIdOverride,
        CancellationToken cancelToken)
    {
        if (!AuthUser.IsAuthenticated())
            return new List<int>();

        var user = await _userStorage.GetUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        if (user == null)
            return new List<int>();

        var roleId = user.RoleId;
        var isMaster = AuthUser.IsMaster || roleId == (int)UserRole.Admin;

        int? effectiveAccountId = user.AccountId is > 0 ? user.AccountId : null;
        if (isMaster && accountIdOverride is > 0)
            effectiveAccountId = accountIdOverride;

        List<Site> accountSites;
        if (effectiveAccountId is > 0)
        {
            accountSites = await _siteStorage
                .GetSitesByAccountAsync(effectiveAccountId.Value, cancelToken)
                .ConfigureAwait(false);
        }
        else if (isMaster)
        {
            accountSites = (await _siteStorage
                .GetSitesAsync(new SiteFilter(), new PagingExDto(10_000), cancelToken)
                .ConfigureAwait(false)).Items;
        }
        else
        {
            return new List<int>();
        }

        var allowed = accountSites.Select(s => s.Id).ToHashSet();

        if (roleId == (int)UserRole.SiteAdmin)
        {
            var assignedSiteIds = user.Site is { Count: > 0 }
                ? user.Site.Select(s => s.Id).ToHashSet()
                : (await _siteStorage.GetSiteIdsForUserAsync(user.Id, cancelToken).ConfigureAwait(false)).ToHashSet();
            if (assignedSiteIds.Count > 0)
                allowed.IntersectWith(assignedSiteIds);
        }

        if (allowed.Count == 0)
            return new List<int>();

        if (siteId > 0)
            return allowed.Contains(siteId) ? new List<int> { siteId } : new List<int>();

        // siteId=0 aggregates all accessible sites (account admin / master / site admin's assigned sites).
        return allowed.OrderBy(id => id).ToList();
    }

    private async Task<DashboardKpisDto> BuildAggregatedKpisAsync(
        IReadOnlyList<int> siteIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string period,
        DateTime utcNow,
        CancellationToken cancelToken)
    {
        var currentCharged = new List<Order>();
        var yesterdayCharged = new List<Order>();
        var sameWeekdayCharged = new List<Order>();

        DateTime yesterdayFrom;
        DateTime yesterdayToEx;
        DateTime sameWeekdayFrom;
        DateTime sameWeekdayToEx;

        if (string.Equals(period, "today", StringComparison.OrdinalIgnoreCase))
        {
            (yesterdayFrom, yesterdayToEx) = ReportPeriodRange.ResolvePreviousIsraelDayRange(utcNow);
            (sameWeekdayFrom, sameWeekdayToEx) = ReportPeriodRange.ResolveSameWeekdayIsraelDayRange(utcNow);
        }
        else
        {
            (yesterdayFrom, yesterdayToEx) = ReportPeriodRange.ResolvePreviousPeriodRange(fromUtc, toUtcExclusive);
            (sameWeekdayFrom, sameWeekdayToEx) = ReportPeriodRange.ShiftRangeByDays(fromUtc, toUtcExclusive, -7);
        }

        foreach (var sid in siteIds)
        {
            currentCharged.AddRange(await _revenueReportStorage
                .GetOrdersInWindowAsync(sid, fromUtc, toUtcExclusive, byChargeDate: true, cancelToken)
                .ConfigureAwait(false));
            yesterdayCharged.AddRange(await _revenueReportStorage
                .GetOrdersInWindowAsync(sid, yesterdayFrom, yesterdayToEx, byChargeDate: true, cancelToken)
                .ConfigureAwait(false));
            sameWeekdayCharged.AddRange(await _revenueReportStorage
                .GetOrdersInWindowAsync(sid, sameWeekdayFrom, sameWeekdayToEx, byChargeDate: true, cancelToken)
                .ConfigureAwait(false));
        }

        var cur = ComputeKpiSnapshot(currentCharged);
        var yest = ComputeKpiSnapshot(yesterdayCharged);
        var week = ComputeKpiSnapshot(sameWeekdayCharged);

        return new DashboardKpisDto
        {
            TotalIncome = cur.Income,
            TotalOrders = cur.Orders,
            AvgOrder = cur.AvgOrder,
            AvgItemsPerOrder = cur.AvgItems,
            VsYesterday = DashboardMetricsHelper.BuildTrend(
                cur.Income, cur.Orders, cur.AvgOrder, cur.AvgItems,
                yest.Income, yest.Orders, yest.AvgOrder, yest.AvgItems),
            VsSameWeekday = DashboardMetricsHelper.BuildTrend(
                cur.Income, cur.Orders, cur.AvgOrder, cur.AvgItems,
                week.Income, week.Orders, week.AvgOrder, week.AvgItems),
        };
    }

    private sealed record KpiSnapshot(decimal Income, int Orders, decimal AvgOrder, decimal AvgItems);

    /// <summary>
    /// All KPIs from orders charged in the period (charge date).
    /// </summary>
    private static KpiSnapshot ComputeKpiSnapshot(List<Order> chargedOrders)
    {
        var chargedCount = chargedOrders.Count;
        var income = DashboardMetricsHelper.Round2(chargedOrders.Sum(o => o.Total ?? 0m));
        var avgOrder = chargedCount > 0 ? DashboardMetricsHelper.Round2(income / chargedCount) : 0m;

        var totalItems = chargedOrders.Sum(o =>
            (o.OrderItem ?? Enumerable.Empty<OrderItem>()).Count(i => !i.IsDeleted && (i.ProductId ?? 0) > 0));
        var avgItems = chargedCount > 0
            ? DashboardMetricsHelper.Round2((decimal)totalItems / chargedCount)
            : 0m;

        return new KpiSnapshot(income, chargedCount, avgOrder, avgItems);
    }

    private async Task<DashboardActiveOrdersDto> BuildActiveOrdersAsync(
        IReadOnlyList<int> siteIds,
        CancellationToken cancelToken)
    {
        var orders = await _dashboardStorage.GetActivePipelineOrdersAsync(siteIds, cancelToken).ConfigureAwait(false);
        return new DashboardActiveOrdersDto
        {
            New = Bucket(orders, "New"),
            InTreatment = Bucket(orders, "InTreatment"),
            Ready = Bucket(orders, "Ready"),
        };
    }

    private static DashboardCountAmountDto Bucket(List<Order> orders, string status)
    {
        var matching = orders
            .Where(o => string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var bucket = new DashboardCountAmountDto();
        foreach (var o in matching)
        {
            bucket.Count++;
            bucket.Amount = DashboardMetricsHelper.Round2(bucket.Amount + (o.Total ?? 0m));
        }

        bucket.PreviewNames = matching
            .OrderByDescending(o => o.Id)
            .Take(3)
            .Select(o =>
            {
                var name = o.CustomerName?.Trim();
                var num = o.OrderNumber?.Trim();
                if (!string.IsNullOrWhiteSpace(num) && !string.IsNullOrWhiteSpace(name))
                    return $"#{num} · {name}";
                if (!string.IsNullOrWhiteSpace(name)) return name!;
                if (!string.IsNullOrWhiteSpace(num)) return $"#{num}";
                return "";
            })
            .Where(s => s.Length > 0)
            .ToList();
        return bucket;
    }

    private static DashboardForwardProjectionDto BuildForwardProjection(List<Order> orders, DateTime utcNow)
    {
        var today = ReportPeriodRange.IsraelCalendarToday(utcNow);
        var tomorrow = today.AddDays(1);
        var dayAfter = today.AddDays(2);
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var endOfWeek = today.AddDays(daysUntilSaturday);

        var todayRemaining = new DashboardCountAmountDto();
        var tomorrowBucket = new DashboardCountAmountDto();
        var restOfWeek = new DashboardCountAmountDto();
        var total = new DashboardCountAmountDto();

        foreach (var o in orders)
        {
            var sched = DashboardMetricsHelper.OrderScheduledDate(o);
            if (sched == null) continue;
            var amount = o.Total ?? 0m;
            total.Count++;
            total.Amount = DashboardMetricsHelper.Round2(total.Amount + amount);

            if (sched.Value == today)
            {
                todayRemaining.Count++;
                todayRemaining.Amount = DashboardMetricsHelper.Round2(todayRemaining.Amount + amount);
            }
            else if (sched.Value == tomorrow)
            {
                tomorrowBucket.Count++;
                tomorrowBucket.Amount = DashboardMetricsHelper.Round2(tomorrowBucket.Amount + amount);
            }
            else if (sched.Value >= dayAfter && sched.Value <= endOfWeek)
            {
                restOfWeek.Count++;
                restOfWeek.Amount = DashboardMetricsHelper.Round2(restOfWeek.Amount + amount);
            }
        }

        return new DashboardForwardProjectionDto
        {
            TodayRemaining = todayRemaining,
            Tomorrow = tomorrowBucket,
            RestOfWeek = restOfWeek,
            PipelineTotal = total,
        };
    }

    private async Task<DashboardInsightsDto> BuildInsightsAsync(
        IReadOnlyList<int> siteIds,
        DashboardKpisDto kpis,
        DashboardForwardProjectionDto projection,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateTime utcNow,
        CancellationToken cancelToken)
    {
        var weekdayAvg = await _dashboardStorage
            .GetWeekdayAverageOrdersAsync(siteIds, ReportPeriodRange.IsraelCalendarToday(utcNow).DayOfWeek, 8, cancelToken)
            .ConfigureAwait(false);

        decimal? prepMinutes = null;
        decimal? deliveryMinutes = null;
        var toDate = toUtcExclusive.AddDays(-1).Date;
        var metricsRes = await _orderService
            .GetOrderHandlingMetricsForSitesAsync(siteIds, fromUtc.Date, toDate, cancelToken)
            .ConfigureAwait(false);
        if (metricsRes.Data != null)
        {
            if (metricsRes.Data.TreatmentHoursMedian.HasValue)
                prepMinutes = Math.Round((decimal)metricsRes.Data.TreatmentHoursMedian.Value * 60m, 0);
            if (metricsRes.Data.ReadyToDeliveryHoursMedian.HasValue)
                deliveryMinutes = Math.Round((decimal)metricsRes.Data.ReadyToDeliveryHoursMedian.Value * 60m, 0);
        }

        var sameWeekdayIncomeBaseline = kpis.VsSameWeekday.IncomePct.HasValue && kpis.TotalIncome > 0
            ? kpis.TotalIncome / (1m + kpis.VsSameWeekday.IncomePct.Value / 100m)
            : 0m;
        var performancePct = DashboardMetricsHelper.PctChange(kpis.TotalIncome, sameWeekdayIncomeBaseline);

        return new DashboardInsightsDto
        {
            WeekdayAvgOrders = weekdayAvg,
            TodayOrders = kpis.TotalOrders,
            TodayIncome = kpis.TotalIncome,
            ExpectedRemainingOrders = projection.TodayRemaining.Count,
            AvgPrepMinutes = prepMinutes,
            AvgDeliveryMinutes = deliveryMinutes,
            PerformanceVsWeekdayPct = performancePct,
            Next3HoursExpectedOrders = projection.TodayRemaining.Count > 0
                ? Math.Max(1, (int)Math.Ceiling(projection.TodayRemaining.Count / 3m))
                : 0,
        };
    }

    private static LiveActivityEventRes MapNewOrderEvent(Order order, Site? site)
    {
        var siteName = site?.SiteName ?? "";
        var amount = order.Total.HasValue ? $"₪{DashboardMetricsHelper.Round2(order.Total.Value):N0}" : null;
        return new LiveActivityEventRes
        {
            Id = $"order-new-{order.Id}-{order.CreationTime.Ticks}",
            Type = "order_new",
            Title = $"הזמנה חדשה · {order.CustomerName ?? order.OrderNumber}",
            Subtitle = amount,
            SiteId = order.SiteId,
            SiteName = siteName,
            OccurredAt = SpecifyUtc(order.CreationTime),
            EntityType = "order",
            EntityId = order.Id,
            Severity = (DateTime.UtcNow - SpecifyUtc(order.CreationTime)).TotalMinutes < 1 ? "highlight" : "normal",
        };
    }

    private static LiveActivityEventRes? MapStatusHistoryEvent(OrderStatusHistory history, Order order, Site? site)
    {
        var status = history.Status.Trim();
        var isPickup = string.Equals(order.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase);
        var type = status switch
        {
            "New" => "order_new",
            "InTreatment" => "order_picked",
            "Ready" => isPickup ? "order_picked" : "order_shipped",
            "Delivered" => "order_delivered",
            "Completed" => "order_delivered",
            "Cancelled" => "order_cancelled",
            _ => null,
        };
        if (type == null) return null;

        var customer = order.CustomerName?.Trim();
        var customerPart = string.IsNullOrWhiteSpace(customer) ? "" : $" · {customer}";
        var title = status switch
        {
            "InTreatment" => $"הזמנה #{order.OrderNumber} לוקטה וחויבה{customerPart}",
            "Ready" => isPickup
                ? $"הזמנה #{order.OrderNumber} מוכנה לאיסוף{customerPart}"
                : $"הזמנה #{order.OrderNumber} יצאה למשלוח{customerPart}",
            "Delivered" => $"הזמנה #{order.OrderNumber} נמסרה{customerPart}",
            "Completed" => $"הזמנה #{order.OrderNumber} נמסרה{customerPart}",
            "Cancelled" => $"הזמנה #{order.OrderNumber} בוטלה",
            _ => $"הזמנה #{order.OrderNumber} · {status}",
        };

        return new LiveActivityEventRes
        {
            Id = $"status-{history.Id}",
            Type = type,
            Title = title,
            Subtitle = order.Total.HasValue ? $"₪{DashboardMetricsHelper.Round2(order.Total.Value):N0}" : null,
            SiteId = order.SiteId,
            SiteName = site?.SiteName ?? "",
            OccurredAt = SpecifyUtc(history.OccurredAt),
            EntityType = "order",
            EntityId = order.Id,
            Severity = type is "order_cancelled" ? "alert" : "normal",
        };
    }

    private static LiveActivityEventRes MapProductUpdateEvent(Product product, Site site)
    {
        var stockStatus = ProductCatalogStockClassification.ClassifyStock(product, account: null);
        var type = stockStatus switch
        {
            "low" => "stock_low",
            "out" => "stock_out",
            _ => "stock_update",
        };
        var stockQty = product.StockQuantity ?? 0m;
        var qtyLabel = product.IsWeighted == true
            ? $"נשארו {DashboardMetricsHelper.Round2(stockQty)} ק\"ג"
            : $"נשארו {DashboardMetricsHelper.Round2(stockQty)} יח'";
        var title = type switch
        {
            "stock_low" => $"מלאי נמוך: {product.Name}",
            "stock_out" => $"אזל מהמלאי: {product.Name}",
            _ => $"עודכן מלאי: {product.Name}",
        };
        string? subtitle = type is "stock_low" or "stock_out" ? qtyLabel : null;

        return new LiveActivityEventRes
        {
            Id = $"product-update-{product.Id}-{product.UpdatedDate?.Ticks ?? 0}",
            Type = type,
            Title = title,
            Subtitle = subtitle,
            SiteId = site.Id,
            SiteName = site.SiteName ?? "",
            OccurredAt = SpecifyUtc(product.UpdatedDate ?? product.CreationTime),
            EntityType = "product",
            EntityId = product.Id,
            Severity = type is "stock_low" or "stock_out" ? "alert" : "normal",
        };
    }

    private static LiveActivityEventRes? MapPaymentEvent(OrderPaymentEvent paymentEvent, Order order, Site? site)
    {
        var code = (paymentEvent.StatusCode ?? "").Trim();
        var success = code is "0" or "000" or "Success";
        var eventType = paymentEvent.EventType.Trim();

        string? type = eventType switch
        {
            "CaptureAuthorization" or "ChargeToken" or "TokenAuthorizationHold" => success ? "payment_received" : "payment_failed",
            "Refund" => success ? "order_refunded" : "payment_failed",
            "Void" => success ? "credit_issued" : "payment_failed",
            _ => null,
        };
        if (type == null) return null;

        var title = type switch
        {
            "payment_received" => $"חיוב התקבל · הזמנה #{order.OrderNumber}",
            "payment_failed" => $"חיוב נכשל · הזמנה #{order.OrderNumber}",
            "order_refunded" => $"הזמנה #{order.OrderNumber} זוכתה",
            "credit_issued" => $"זיכוי הופק · הזמנה #{order.OrderNumber}",
            _ => $"תשלום · הזמנה #{order.OrderNumber}",
        };

        var amount = paymentEvent.Amount ?? order.Total;
        return new LiveActivityEventRes
        {
            Id = $"payment-{paymentEvent.Id}",
            Type = type,
            Title = title,
            Subtitle = amount.HasValue ? $"₪{DashboardMetricsHelper.Round2(amount.Value):N0}" : null,
            SiteId = order.SiteId,
            SiteName = site?.SiteName ?? "",
            OccurredAt = SpecifyUtc(paymentEvent.CreationTime),
            EntityType = "payment",
            EntityId = order.Id,
            Severity = type == "payment_failed" ? "alert" : "normal",
        };
    }

    private static DateTime SpecifyUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static (DateTime from, DateTime toExclusive) ResolveDeliveryWindow(string window, DateTime utcNow)
    {
        var today = ReportPeriodRange.IsraelCalendarToday(utcNow);
        var w = (window ?? "today").Trim().ToLowerInvariant();
        return w switch
        {
            "tomorrow" => (today.AddDays(1), today.AddDays(2)),
            "week" => (today, today.AddDays(7)),
            _ => (today, today.AddDays(1)),
        };
    }

}
