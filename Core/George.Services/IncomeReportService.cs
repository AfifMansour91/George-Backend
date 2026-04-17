using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    public class IncomeReportService : ServiceBase
    {
        private readonly IncomeReportStorage _incomeReportStorage;
        private readonly CategoryStorage _categoryStorage;

        public IncomeReportService(
            ILogger<IncomeReportService> logger,
            IMapper mapper,
            CacheManager cache,
            IncomeReportStorage incomeReportStorage,
            CategoryStorage categoryStorage)
            : base(logger, mapper, cache)
        {
            _incomeReportStorage = incomeReportStorage;
            _categoryStorage = categoryStorage;
        }

        public async Task<IApiResponse<IncomeReportRes>> GetReportAsync(
            int siteId,
            string period,
            DateTime? customFrom,
            DateTime? customTo,
            int? categoryId,
            string? coupon,
            string? kpiCompare,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<IncomeReportRes>();
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

            var utcNow = DateTime.UtcNow;
            DateTime fromUtc;
            DateTime toUtcExclusive;
            try
            {
                (fromUtc, toUtcExclusive) = ResolveCurrentRange(period, customFrom, customTo, utcNow);
            }
            catch
            {
                return CreateResponse(response, StatusCode.InvalidRequest, "Invalid period or custom dates.");
            }
            if (toUtcExclusive <= fromUtc)
                return CreateResponse(response, StatusCode.InvalidRequest, "Invalid date range.");

            var (baselineFrom, baselineToEx) = ResolveBaselineRange(period, fromUtc, toUtcExclusive, kpiCompare);

            var couponTrim = string.IsNullOrWhiteSpace(coupon) ? null : coupon.Trim();

            var currentOrders = await _incomeReportStorage.GetReportOrdersAsync(siteId, fromUtc, toUtcExclusive, couponTrim, cancelToken)
                .ConfigureAwait(false);
            var baselineOrders = await _incomeReportStorage.GetReportOrdersAsync(siteId, baselineFrom, baselineToEx, couponTrim, cancelToken)
                .ConfigureAwait(false);

            var productIds = currentOrders.SelectMany(o => o.OrderItem).Select(i => i.ProductId ?? 0)
                .Concat(baselineOrders.SelectMany(o => o.OrderItem).Select(i => i.ProductId ?? 0))
                .Where(id => id > 0).Distinct();
            var products = await _incomeReportStorage.GetProductsWithCategoriesAsync(productIds, cancelToken).ConfigureAwait(false);

            var priorCustomerIds = await _incomeReportStorage.GetCustomerIdsWithPriorPaidCompletedOrdersAsync(siteId, fromUtc, cancelToken)
                .ConfigureAwait(false);

            var catFilter = categoryId is > 0 ? categoryId : null;

            var res = new IncomeReportRes
            {
                CurrentRange = new IncomeReportRangeDto { FromUtc = fromUtc, ToUtcExclusive = toUtcExclusive },
                BaselineRange = new IncomeReportRangeDto { FromUtc = baselineFrom, ToUtcExclusive = baselineToEx },
            };

            var categories = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId, IsEnabled = true },
                new PagingExDto(10_000) { IncludeTotal = false, Skip = 0 },
                cancelToken).ConfigureAwait(false);
            res.Categories = categories.Items
                .Where(c => !c.IsDeleted && c.IsActive)
                .Select(c => new IncomeReportCategoryOptionDto { Id = c.Id, Name = c.Name })
                .OrderBy(c => c.Name)
                .ToList();

            res.Kpis = BuildKpis(currentOrders, baselineOrders, products, catFilter, priorCustomerIds);
            res.DayRows = BuildDayRows(currentOrders, products, catFilter, fromUtc, toUtcExclusive);
            res.DayTotals = SumDayTotals(res.DayRows);
            res.OrderRows = BuildOrderRows(currentOrders, products, catFilter);
            res.Segments = BuildSegments(currentOrders, products, catFilter);
            res.TopProducts = BuildTopProducts(currentOrders, baselineOrders, products, catFilter);

            response.Data = res;
            return response;
        }

        private static (DateTime fromUtc, DateTime toUtcExclusive) ResolveCurrentRange(
            string period,
            DateTime? customFrom,
            DateTime? customTo,
            DateTime utcNow)
        {
            var p = (period ?? "month").Trim().ToLowerInvariant();
            var today = utcNow.Date;
            switch (p)
            {
                case "today":
                    return (today, today.AddDays(1));
                case "week":
                    return (today.AddDays(-6), today.AddDays(1));
                case "month":
                    return (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), today.AddDays(1));
                case "custom":
                    if (customFrom == null || customTo == null)
                        throw new ArgumentException("customFrom/customTo required for custom period.");
                    var from = DateTime.SpecifyKind(customFrom.Value.Date, DateTimeKind.Utc);
                    var toEx = DateTime.SpecifyKind(customTo.Value.Date.AddDays(1), DateTimeKind.Utc);
                    return (from, toEx);
                default:
                    return (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), today.AddDays(1));
            }
        }

        private static (DateTime baselineFrom, DateTime baselineToExclusive) ResolveBaselineRange(
            string period,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            string? kpiCompare)
        {
            var p = (period ?? "month").Trim().ToLowerInvariant();
            if (p == "today")
            {
                var cmp = (kpiCompare ?? "same_weekday").Trim().ToLowerInvariant();
                if (cmp == "previous_day" || cmp == "previous_calendar_day")
                {
                    var day = fromUtc.Date.AddDays(-1);
                    return (day, day.AddDays(1));
                }
                var d = fromUtc.Date.AddDays(-7);
                return (d, d.AddDays(1));
            }

            var len = toUtcExclusive - fromUtc;
            return (fromUtc - len, fromUtc);
        }

        private static decimal LineMerchandise(OrderItem i)
        {
            if (i.PickedQuantity is > 0m)
            {
                if (i.TotalPrice.HasValue) return i.TotalPrice.Value;
                return i.PickedQuantity.Value * (i.PricePerUnit ?? 0m);
            }
            if (i.TotalPrice.HasValue) return i.TotalPrice.Value;
            return i.Quantity * (i.PricePerUnit ?? 0m);
        }

        private static decimal OrderLinesMerch(Order o) =>
            o.OrderItem?.Sum(LineMerchandise) ?? 0m;

        private static decimal OrderIncome(Order o) => o.Total ?? 0m;

        private static decimal OrderShipping(Order o) => o.ShippingCost ?? 0m;

        private static decimal OrderDiscount(Order o)
        {
            var sub = o.SubTotal ?? 0m;
            var ship = o.ShippingCost ?? 0m;
            var tot = o.Total;
            if (!tot.HasValue) return 0m;
            var d = sub + ship - tot.Value;
            return d > 0m ? d : 0m;
        }

        private static int? PrimaryCategoryId(Product? p)
        {
            if (p?.ProductCategory == null || p.ProductCategory.Count == 0) return null;
            var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
            if (primary != null) return primary.CategoryId;
            return p.ProductCategory.First().CategoryId;
        }

        private static decimal CategoryLineMerchSum(Order o, Dictionary<int, Product> products, int categoryId)
        {
            decimal s = 0m;
            foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
            {
                if (line.ProductId is not > 0) continue;
                if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                if (PrimaryCategoryId(p) != categoryId) continue;
                s += LineMerchandise(line);
            }
            return s;
        }

        /// <summary>Share of order income attributed to category (0..1). Full order when categoryId null.</summary>
        private static decimal CategoryIncomeShare(Order o, Dictionary<int, Product> products, int? categoryId)
        {
            if (categoryId == null) return 1m;
            var totalMerch = OrderLinesMerch(o);
            if (totalMerch <= 0m) return 0m;
            var catMerch = CategoryLineMerchSum(o, products, categoryId.Value);
            if (catMerch <= 0m) return 0m;
            return catMerch / totalMerch;
        }

        private static bool OrderHasCategory(Order o, Dictionary<int, Product> products, int categoryId) =>
            CategoryLineMerchSum(o, products, categoryId) > 0m;

        private static IncomeReportKpisDto BuildKpis(
            List<Order> current,
            List<Order> baseline,
            Dictionary<int, Product> products,
            int? categoryId,
            HashSet<int> priorCustomerIds)
        {
            decimal AllocIncome(Order o) => OrderIncome(o) * CategoryIncomeShare(o, products, categoryId);
            decimal AllocQty(Order o)
            {
                var sh = CategoryIncomeShare(o, products, categoryId);
                if (sh <= 0m) return 0m;
                var qty = o.OrderItem?.Sum(i => i.Quantity) ?? 0m;
                return qty * sh;
            }

            var curOrders = current.Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value)).ToList();
            var baseOrders = baseline.Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value)).ToList();

            var totalIncome = curOrders.Sum(AllocIncome);
            var totalUnfiltered = current.Sum(OrderIncome);
            var baseIncome = baseOrders.Sum(AllocIncome);

            var cCount = curOrders.Count;
            var bCount = baseOrders.Count;
            var avgOrder = cCount > 0 ? totalIncome / cCount : 0m;
            var avgBase = bCount > 0 ? baseIncome / bCount : 0m;
            var avgItems = cCount > 0 ? curOrders.Sum(AllocQty) / cCount : 0m;
            var avgItemsB = bCount > 0 ? baseOrders.Sum(AllocQty) / bCount : 0m;

            decimal retInc = 0m, newInc = 0m;
            foreach (var o in curOrders)
            {
                var inc = AllocIncome(o);
                if (o.CustomerId.HasValue && priorCustomerIds.Contains(o.CustomerId.Value))
                    retInc += inc;
                else
                    newInc += inc;
            }
            var denom = retInc + newInc;
            var retPct = denom > 0m ? (retInc / denom) * 100m : 0m;

            return new IncomeReportKpisDto
            {
                TotalIncome = Round2(totalIncome),
                TotalIncomeUnfiltered = Round2(totalUnfiltered),
                TotalIncomeBaseline = Round2(baseIncome),
                AvgOrder = Round2(avgOrder),
                AvgOrderBaseline = Round2(avgBase),
                AvgItemsPerOrder = Round2(avgItems),
                AvgItemsPerOrderBaseline = Round2(avgItemsB),
                ReturningPct = Round2(retPct),
                ReturningIncome = Round2(retInc),
                NewIncome = Round2(newInc),
            };
        }

        private static List<IncomeReportDayRowDto> BuildDayRows(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId,
            DateTime fromUtc,
            DateTime toUtcExclusive)
        {
            _ = fromUtc;
            _ = toUtcExclusive;
            var dayKeys = orders.Select(o => o.CreationTime.Date).Distinct().OrderBy(d => d);
            var rows = new List<IncomeReportDayRowDto>();
            foreach (var day in dayKeys)
            {
                var dayOrdersAll = orders.Where(o => o.CreationTime.Date == day).ToList();
                var dayOrdersFiltered = dayOrdersAll
                    .Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value))
                    .ToList();

                decimal income = 0m, incomeTotal = 0m;
                var orderIds = new HashSet<int>();
                decimal delRev = 0m, pickRev = 0m, ship = 0m, disc = 0m;
                var delOrd = new HashSet<int>();
                var pickOrd = new HashSet<int>();

                foreach (var o in dayOrdersAll)
                    incomeTotal += OrderIncome(o);

                foreach (var o in dayOrdersFiltered)
                {
                    var sh = CategoryIncomeShare(o, products, categoryId);
                    if (sh <= 0m) continue;
                    income += OrderIncome(o) * sh;
                    orderIds.Add(o.Id);
                    ship += OrderShipping(o) * sh;
                    disc += OrderDiscount(o) * sh;
                    var merch = OrderLinesMerch(o) * sh;
                    if (string.Equals(o.DeliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                    {
                        delRev += merch;
                        delOrd.Add(o.Id);
                    }
                    else
                    {
                        pickRev += merch;
                        pickOrd.Add(o.Id);
                    }
                }

                decimal? pctOfDay = null;
                if (categoryId != null && incomeTotal > 0m)
                    pctOfDay = Round2(income / incomeTotal * 100m);

                rows.Add(new IncomeReportDayRowDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Label = day.ToString("dd/MM"),
                    Income = Round2(income),
                    IncomeDayTotalUnfiltered = Round2(incomeTotal),
                    PctOfDay = pctOfDay,
                    Orders = orderIds.Count,
                    DeliveryProductRevenue = Round2(delRev),
                    DeliveryOrders = delOrd.Count,
                    ShippingFees = Round2(ship),
                    PickupProductRevenue = Round2(pickRev),
                    PickupOrders = pickOrd.Count,
                    Discounts = Round2(disc),
                });
            }

            return rows;
        }

        private static IncomeReportDayTotalsDto SumDayTotals(List<IncomeReportDayRowDto> rows) =>
            new IncomeReportDayTotalsDto
            {
                Income = Round2(rows.Sum(r => r.Income)),
                Orders = rows.Sum(r => r.Orders),
                DeliveryProductRevenue = Round2(rows.Sum(r => r.DeliveryProductRevenue)),
                DeliveryOrders = rows.Sum(r => r.DeliveryOrders),
                ShippingFees = Round2(rows.Sum(r => r.ShippingFees)),
                PickupProductRevenue = Round2(rows.Sum(r => r.PickupProductRevenue)),
                PickupOrders = rows.Sum(r => r.PickupOrders),
                Discounts = Round2(rows.Sum(r => r.Discounts)),
            };

        private static List<IncomeReportOrderRowDto> BuildOrderRows(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var list = orders
                .Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value))
                .OrderBy(o => o.CreationTime)
                .ToList();

            return list.Select(o =>
            {
                var sh = CategoryIncomeShare(o, products, categoryId);
                return new IncomeReportOrderRowDto
                {
                    OrderId = o.Id,
                    OrderDate = o.CreationTime,
                    CustomerName = o.CustomerName ?? "",
                    Source = o.Source ?? "",
                    Income = Round2(OrderIncome(o) * sh),
                    ShippingFee = Round2(OrderShipping(o) * sh),
                    Discount = Round2(OrderDiscount(o) * sh),
                    DeliveryType = o.DeliveryType ?? "",
                };
            }).ToList();
        }

        private static IncomeReportSegmentsDto BuildSegments(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var relevant = orders.Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value)).ToList();
            decimal dInc = 0m, pInc = 0m;
            var bySource = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var o in relevant)
            {
                var sh = CategoryIncomeShare(o, products, categoryId);
                var inc = OrderIncome(o) * sh;
                if (string.Equals(o.DeliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                    dInc += inc;
                else
                    pInc += inc;

                var sk = MapSourceKey(o.Source);
                if (!bySource.ContainsKey(sk)) bySource[sk] = 0m;
                bySource[sk] += inc;
            }

            var tot = dInc + pInc;
            var dto = new IncomeReportSegmentsDto
            {
                DeliveryPct = tot > 0m ? Round2(dInc / tot * 100m) : 0m,
                PickupPct = tot > 0m ? Round2(pInc / tot * 100m) : 0m,
            };

            var srcTotal = bySource.Values.Sum();
            if (srcTotal > 0m)
            {
                foreach (var kv in bySource.OrderByDescending(kv => kv.Value))
                {
                    dto.SourceSlices.Add(new IncomeReportSourceSliceDto
                    {
                        Key = kv.Key,
                        Name = kv.Key,
                        Pct = Round2(kv.Value / srcTotal * 100m),
                        Income = Round2(kv.Value),
                    });
                }
            }

            var orderHour = relevant
                .GroupBy(o => o.CreationTime.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (orderHour != null)
                dto.PeakOrderHourLabel = $"{orderHour.Key:00}:00–{orderHour.Key + 1:00}:00";

            var delHour = relevant
                .Where(o => o.DeliveryDate != null)
                .GroupBy(o => o.DeliveryDate!.Value.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (delHour != null)
                dto.PeakDeliveryHourLabel = $"{delHour.Key:00}:00–{delHour.Key + 1:00}:00";

            return dto;
        }

        private static string MapSourceKey(string? source)
        {
            var s = (source ?? "").Trim();
            if (string.Equals(s, "WooCommerce", StringComparison.OrdinalIgnoreCase))
                return "Website";
            return s.Length > 0 ? s : "Other";
        }

        private static List<IncomeReportTopProductDto> BuildTopProducts(
            List<Order> current,
            List<Order> baseline,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var cur = AggregateProductRevenue(current, products, categoryId);
            var bas = AggregateProductRevenue(baseline, products, categoryId);

            return cur.OrderByDescending(kv => kv.Value.revenue)
                .Take(5)
                .Select(kv =>
                {
                    var t = kv.Value;
                    var baseRev = bas.TryGetValue(kv.Key, out var br) ? br.revenue : 0m;
                    var trendUp = t.revenue >= baseRev;
                    return new IncomeReportTopProductDto
                    {
                        ProductId = kv.Key,
                        Name = t.name,
                        CategoryName = t.cat,
                        QuantityLabel = FormatQty(t.qty),
                        Revenue = Round2(t.revenue),
                        TrendUp = trendUp,
                    };
                })
                .ToList();
        }

        private static Dictionary<int, (string name, string cat, decimal qty, decimal revenue)> AggregateProductRevenue(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var map = new Dictionary<int, (string name, string cat, decimal qty, decimal revenue)>();
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (categoryId != null && cid != categoryId) continue;
                    var merch = LineMerchandise(line);
                    if (merch <= 0m) continue;
                    var share = OrderLinesMerch(o) > 0m ? merch / OrderLinesMerch(o) : 0m;
                    var alloc = OrderIncome(o) * share;
                    var catName = p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? "";
                    if (!map.ContainsKey(p.Id))
                        map[p.Id] = (p.Name, catName, 0m, 0m);
                    var t = map[p.Id];
                    map[p.Id] = (t.name, string.IsNullOrEmpty(t.cat) ? catName : t.cat, t.qty + line.Quantity, t.revenue + alloc);
                }
            }
            return map;
        }

        private static string FormatQty(decimal q) =>
            q == Math.Floor(q) ? $"{(int)q} יח'" : $"{Round2(q)}";

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    }
}
