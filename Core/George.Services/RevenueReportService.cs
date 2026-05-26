using System.Globalization;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    public class RevenueReportService : ServiceBase
    {
        private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

        private readonly RevenueReportStorage _storage;
        private readonly CategoryStorage _categoryStorage;

        public RevenueReportService(
            ILogger<RevenueReportService> logger,
            IMapper mapper,
            CacheManager cache,
            RevenueReportStorage storage,
            CategoryStorage categoryStorage)
            : base(logger, mapper, cache)
        {
            _storage = storage;
            _categoryStorage = categoryStorage;
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

        public async Task<IApiResponse<RevenueReportRes>> GetReportAsync(
            int siteId,
            string period,
            DateTime? customFrom,
            DateTime? customTo,
            string dateBasis = "charge",
            string? compare = null,
            string? search = null,
            string? channels = null,
            string? paymentMethods = null,
            string? statuses = null,
            string? cities = null,
            string? categoryIds = null,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<RevenueReportRes>();
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

            var byCharge = !string.Equals(dateBasis, "order", StringComparison.OrdinalIgnoreCase);
            var (baselineFrom, baselineToEx) = ResolveBaselineRange(period, fromUtc, toUtcExclusive, compare);

            var channelFilter = ParseCsvKeys(channels);
            var paymentFilter = ParseCsvKeys(paymentMethods);
            var statusFilter = ParseCsvKeys(statuses);
            var cityFilter = ParseCsvKeys(cities);
            var categoryFilter = ParseIntCsv(categoryIds);

            var currentAll = await _storage.GetOrdersInWindowAsync(siteId, fromUtc, toUtcExclusive, byCharge, cancelToken)
                .ConfigureAwait(false);
            var baselineAll = await _storage.GetOrdersInWindowAsync(siteId, baselineFrom, baselineToEx, byCharge, cancelToken)
                .ConfigureAwait(false);

            var current = ApplyFilters(currentAll, search, channelFilter, paymentFilter, statusFilter, cityFilter, categoryFilter, null);
            var baseline = ApplyFilters(baselineAll, search, channelFilter, paymentFilter, statusFilter, cityFilter, categoryFilter, null);

            var productIds = current.SelectMany(o => o.OrderItem).Select(i => i.ProductId ?? 0)
                .Concat(baseline.SelectMany(o => o.OrderItem).Select(i => i.ProductId ?? 0))
                .Where(id => id > 0).Distinct();
            var products = await _storage.GetProductsWithCategoriesAsync(productIds, cancelToken).ConfigureAwait(false);

            var grouping = ResolveGrouping(fromUtc, toUtcExclusive);
            var res = new RevenueReportRes
            {
                CurrentRange = new RevenueReportRangeDto { FromUtc = fromUtc, ToUtcExclusive = toUtcExclusive },
                BaselineRange = new RevenueReportRangeDto { FromUtc = baselineFrom, ToUtcExclusive = baselineToEx },
                Grouping = grouping,
            };

            var categories = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId, IsEnabled = true },
                new PagingExDto(10_000) { IncludeTotal = false, Skip = 0 },
                cancelToken).ConfigureAwait(false);
            res.Categories = categories.Items
                .Where(c => !c.IsDeleted && c.IsActive)
                .Select(c => new RevenueReportCategoryOptionDto { Id = c.Id, Name = c.Name })
                .OrderBy(c => c.Name)
                .ToList();

            res.Cities = BuildCityOptions(currentAll);
            res.Channels = BuildChannelOptions(currentAll);
            res.PaymentMethods = BuildPaymentOptions();
            res.Statuses = BuildStatusOptions();

            res.Kpis = BuildKpis(current, baseline, byCharge);
            if (byCharge)
            {
                var pipelineOrders = await _storage.GetPipelineOrdersAsync(siteId, cancelToken).ConfigureAwait(false);
                res.Pipeline = BuildPipeline(pipelineOrders);
            }

            res.TrendPoints = BuildTrend(current, fromUtc, toUtcExclusive, grouping, byCharge);
            res.BaselineTrendPoints = BuildTrend(baseline, baselineFrom, baselineToEx, grouping, byCharge);
            res.DayRows = BuildDayRows(current, fromUtc, toUtcExclusive, grouping, byCharge);
            res.DayTotals = SumDayTotals(res.DayRows);
            res.OrderRows = BuildOrderRows(current, byCharge);
            res.Segments = BuildSegments(current, products, categoryFilter);

            response.Data = res;
            return response;
        }

        private static (DateTime fromUtc, DateTime toUtcExclusive) ResolveCurrentRange(
            string period, DateTime? customFrom, DateTime? customTo, DateTime utcNow)
        {
            var p = (period ?? "month").Trim().ToLowerInvariant();
            var today = utcNow.Date;
            return p switch
            {
                "today" => (today, today.AddDays(1)),
                "week" => ResolveWeekRange(today),
                "custom" when customFrom != null && customTo != null =>
                    (DateTime.SpecifyKind(customFrom.Value.Date, DateTimeKind.Utc),
                        DateTime.SpecifyKind(customTo.Value.Date.AddDays(1), DateTimeKind.Utc)),
                _ => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), today.AddDays(1)),
            };
        }

        private static (DateTime from, DateTime toEx) ResolveWeekRange(DateTime today)
        {
            var dow = (int)today.DayOfWeek;
            var daysFromMonday = dow == (int)DayOfWeek.Sunday ? 6 : dow - (int)DayOfWeek.Monday;
            var monday = today.AddDays(-daysFromMonday);
            return (monday, today.AddDays(1));
        }

        private static (DateTime baselineFrom, DateTime baselineToExclusive) ResolveBaselineRange(
            string period, DateTime fromUtc, DateTime toUtcExclusive, string? compare)
        {
            var cmp = (compare ?? "prev_month").Trim().ToLowerInvariant();
            if (cmp is "prev_year" or "year" or "last_year")
            {
                return (fromUtc.AddYears(-1), toUtcExclusive.AddYears(-1));
            }

            if (cmp is "prev_month" or "month")
            {
                return (fromUtc.AddMonths(-1), toUtcExclusive.AddMonths(-1));
            }

            var len = toUtcExclusive - fromUtc;
            return (fromUtc - len, fromUtc);
        }

        private static string ResolveGrouping(DateTime fromUtc, DateTime toUtcExclusive)
        {
            var days = (toUtcExclusive - fromUtc).TotalDays;
            if (days <= 31) return "daily";
            if (days <= 92) return "weekly";
            return "monthly";
        }

        private static HashSet<string> ParseCsvKeys(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static HashSet<int> ParseIntCsv(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new HashSet<int>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToHashSet();

        private static DateTime AssumeUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        /// <summary>When the order was actually charged (never falls back to creation for unpaid rows).</summary>
        private static DateTime? ChargeDateUtc(Order o)
        {
            if (o.PaidAt != null)
                return AssumeUtc(o.PaidAt.Value);
            if (string.Equals(o.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) && o.UpdatedDate != null)
                return AssumeUtc(o.UpdatedDate.Value);
            return null;
        }

        private static DateTime ReportDate(Order o, bool byCharge)
        {
            if (!byCharge)
                return AssumeUtc(o.CreationTime);
            return ChargeDateUtc(o) ?? AssumeUtc(o.CreationTime);
        }

        private static bool IsCancelled(Order o) =>
            string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool HasCredit(Order o) =>
            !string.IsNullOrWhiteSpace(o.RefundInvoiceNumber);

        private static bool IsFullCredit(Order o) =>
            string.Equals(o.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase);

        private static decimal OrderTotal(Order o) => o.Total ?? 0m;

        private static decimal OrderDiscount(Order o)
        {
            var sub = o.SubTotal ?? 0m;
            var ship = o.ShippingCost ?? 0m;
            var tot = o.Total;
            if (!tot.HasValue) return 0m;
            var d = sub + ship - tot.Value;
            return d > 0m ? d : 0m;
        }

        private static decimal ChargedAmount(Order o) =>
            IsCancelled(o) ? 0m : (string.Equals(o.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? OrderTotal(o) : 0m);

        private static decimal CreditAmount(Order o) => HasCredit(o) ? OrderTotal(o) : 0m;

        private static decimal CancellationAmount(Order o) => IsCancelled(o) ? OrderTotal(o) : 0m;

        private static string MapDisplayStatus(Order o)
        {
            if (IsCancelled(o)) return "cancelled";
            if (HasCredit(o)) return "credited";
            if (string.Equals(o.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                && (o.Status is "Completed" or "Delivered" or "Ready"))
                return "delivered";
            return "pending";
        }

        private static string MapPaymentKey(Order o)
        {
            var m = (o.PaymentMethod ?? "").Trim();
            if (string.Equals(m, "Cash", StringComparison.OrdinalIgnoreCase)) return "cash";
            if (m.Contains("Bit", StringComparison.OrdinalIgnoreCase)) return "bit";
            if (m.Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("Bank", StringComparison.OrdinalIgnoreCase)) return "transfer";
            if (string.Equals(m, "SavedCard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "CreditSms", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "CreditPhone", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(m) && !string.Equals(m, "Cash", StringComparison.OrdinalIgnoreCase)))
                return "credit";
            return "credit";
        }

        private static string MapChannelKey(Order o)
        {
            var s = (o.Source ?? "").Trim();
            if (string.Equals(s, "WooCommerce", StringComparison.OrdinalIgnoreCase)) return "website";
            return s.Length > 0 ? s.ToLowerInvariant() : "other";
        }

        private static List<Order> ApplyFilters(
            List<Order> orders,
            string? search,
            HashSet<string> channels,
            HashSet<string> payments,
            HashSet<string> statuses,
            HashSet<string> cities,
            HashSet<int> categories,
            Dictionary<int, Product>? products)
        {
            IEnumerable<Order> q = orders;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(o =>
                    (o.CustomerName != null && o.CustomerName.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (o.OrderNumber != null && o.OrderNumber.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (o.CouponCode != null && o.CouponCode.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    o.OrderItem.Any(i => i.Title != null && i.Title.Contains(s, StringComparison.OrdinalIgnoreCase)));
            }

            if (channels.Count > 0)
                q = q.Where(o => channels.Contains(MapChannelKey(o)));
            if (payments.Count > 0)
                q = q.Where(o => payments.Contains(MapPaymentKey(o)));
            if (statuses.Count > 0)
                q = q.Where(o => statuses.Contains(MapDisplayStatus(o)));
            if (cities.Count > 0)
            {
                q = q.Where(o =>
                {
                    var city = o.DeliveryCity?.Trim() ?? "";
                    if (string.IsNullOrEmpty(city))
                        return cities.Contains(RevenueReportStorage.CityEmptyFilterKey);
                    return cities.Contains(city, StringComparer.OrdinalIgnoreCase);
                });
            }

            return q.ToList();
        }

        private static RevenueReportKpisDto BuildKpis(List<Order> current, List<Order> baseline, bool byCharge)
        {
            var charged = current.Sum(ChargedAmount);
            var credits = current.Sum(CreditAmount);
            var cancels = current.Sum(CancellationAmount);
            var net = charged - credits - cancels;
            var orderCount = byCharge
                ? current.Count(o => ChargedAmount(o) > 0 || IsCancelled(o) || HasCredit(o))
                : current.Count;

            var bCharged = baseline.Sum(ChargedAmount);
            var bCredits = baseline.Sum(CreditAmount);
            var bCancels = baseline.Sum(CancellationAmount);
            var bNet = bCharged - bCredits - bCancels;
            var bOrderCount = byCharge
                ? baseline.Count(o => ChargedAmount(o) > 0 || IsCancelled(o) || HasCredit(o))
                : baseline.Count;

            var partial = current.Count(o => HasCredit(o) && !IsFullCredit(o));
            var full = current.Count(o => HasCredit(o) && IsFullCredit(o));
            var cancelPct = orderCount > 0 ? Math.Round(100m * current.Count(IsCancelled) / orderCount, 1) : 0m;

            return new RevenueReportKpisDto
            {
                NetRevenue = Round2(net),
                NetRevenueBaseline = Round2(bNet),
                OrderCount = orderCount,
                OrderCountBaseline = bOrderCount,
                CreditsAmount = Round2(credits),
                CreditsPartialCount = partial,
                CreditsFullCount = full,
                CreditsAmountBaseline = Round2(bCredits),
                CancellationsAmount = Round2(cancels),
                CancellationsOrderPct = cancelPct,
                CancellationsAmountBaseline = Round2(bCancels),
            };
        }

        private static RevenueReportPipelineDto BuildPipeline(List<Order> orders)
        {
            if (orders.Count == 0)
                return new RevenueReportPipelineDto();
            var amount = orders.Sum(OrderTotal);
            var now = DateTime.UtcNow;
            var avgDays = orders.Average(o =>
            {
                var created = AssumeUtc(o.CreationTime);
                return Math.Max(0, (now - created).TotalDays);
            });
            return new RevenueReportPipelineDto
            {
                PendingChargeCount = orders.Count,
                PendingChargeAmount = Round2(amount),
                AvgDaysToCharge = Round2((decimal)avgDays),
            };
        }

        private static List<RevenueReportTrendPointDto> BuildTrend(
            List<Order> orders, DateTime fromUtc, DateTime toUtcExclusive, string grouping, bool byCharge)
        {
            var buckets = new Dictionary<string, (string label, decimal income)>();
            foreach (var o in orders)
            {
                var dt = ReportDate(o, byCharge);
                var key = BucketKey(dt, grouping);
                var label = BucketLabel(dt, grouping);
                var add = ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o);
                if (!buckets.ContainsKey(key))
                    buckets[key] = (label, 0m);
                var cur = buckets[key];
                buckets[key] = (cur.label, cur.income + add);
            }

            return EnumerateBuckets(fromUtc, toUtcExclusive, grouping)
                .Select(k =>
                {
                    var has = buckets.TryGetValue(k.key, out var b);
                    return new RevenueReportTrendPointDto
                    {
                        Date = k.date,
                        Label = has ? b.label : k.label,
                        Income = Round2(has ? b.income : 0m),
                    };
                })
                .ToList();
        }

        private static List<RevenueReportDayRowDto> BuildDayRows(
            List<Order> orders, DateTime fromUtc, DateTime toUtcExclusive, string grouping, bool byCharge)
        {
            if (grouping != "daily")
                return BuildTrend(orders, fromUtc, toUtcExclusive, grouping, byCharge)
                    .Select(t => new RevenueReportDayRowDto
                    {
                        Date = t.Date,
                        Label = t.Label,
                        Revenue = t.Income,
                        Orders = orders.Count(o => BucketKey(ReportDate(o, byCharge), grouping) == t.Date),
                    })
                    .ToList();

            return EnumerateBuckets(fromUtc, toUtcExclusive, "daily").Select(k =>
            {
                var dayOrders = orders.Where(o => BucketKey(ReportDate(o, byCharge), "daily") == k.key).ToList();
                var charged = dayOrders.Sum(ChargedAmount);
                var credits = dayOrders.Sum(CreditAmount);
                var cancels = dayOrders.Sum(CancellationAmount);
                return new RevenueReportDayRowDto
                {
                    Date = k.date,
                    Label = k.label,
                    Orders = dayOrders.Count,
                    Revenue = Round2(charged - credits - cancels),
                    Credits = Round2(credits),
                    Cancellations = Round2(cancels),
                    Discounts = Round2(dayOrders.Sum(OrderDiscount)),
                };
            }).ToList();
        }

        private static RevenueReportDayTotalsDto SumDayTotals(List<RevenueReportDayRowDto> rows) =>
            new()
            {
                Orders = rows.Sum(r => r.Orders),
                Revenue = Round2(rows.Sum(r => r.Revenue)),
                Credits = Round2(rows.Sum(r => r.Credits)),
                Cancellations = Round2(rows.Sum(r => r.Cancellations)),
                Discounts = Round2(rows.Sum(r => r.Discounts)),
            };

        private static List<RevenueReportOrderRowDto> BuildOrderRows(List<Order> orders, bool byCharge) =>
            orders
                .OrderByDescending(o => ReportDate(o, byCharge))
                .Select(o => new RevenueReportOrderRowDto
                {
                    OrderId = o.Id,
                    OrderNumber = o.OrderNumber ?? "",
                    OrderDate = ReportDate(o, byCharge).Kind == DateTimeKind.Utc
                        ? ReportDate(o, byCharge)
                        : DateTime.SpecifyKind(ReportDate(o, byCharge), DateTimeKind.Utc),
                    CustomerName = o.CustomerName ?? "",
                    Source = MapChannelKey(o),
                    PaymentMethod = MapPaymentKey(o),
                    Status = MapDisplayStatus(o),
                    StatusReason = o.ManagerNote,
                    Total = Round2(OrderTotal(o)),
                    InvoiceUrl = string.IsNullOrWhiteSpace(o.CardcomDocumentUrl) ? null : o.CardcomDocumentUrl.Trim(),
                    RefundInvoiceUrl = string.IsNullOrWhiteSpace(o.CardcomRefundDocumentUrl)
                        ? null
                        : o.CardcomRefundDocumentUrl.Trim(),
                    IsCancelled = IsCancelled(o),
                })
                .ToList();

        private static RevenueReportSegmentsDto BuildSegments(
            List<Order> orders,
            Dictionary<int, Product> products,
            HashSet<int> categoryFilter)
        {
            var netTotal = orders.Sum(o => ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o));
            if (netTotal <= 0) netTotal = 1m;

            var paymentGroups = orders
                .GroupBy(MapPaymentKey)
                .Select(g => new RevenueReportSliceDto
                {
                    Key = g.Key,
                    Name = g.Key,
                    Income = Round2(g.Sum(o => ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o))),
                    OrderCount = g.Count(),
                })
                .ToList();
            ApplyPct(paymentGroups, netTotal);

            var channelGroups = orders
                .GroupBy(MapChannelKey)
                .Select(g => new RevenueReportSliceDto
                {
                    Key = g.Key,
                    Name = g.Key,
                    Income = Round2(g.Sum(o => ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o))),
                    OrderCount = g.Count(),
                })
                .ToList();
            ApplyPct(channelGroups, netTotal);

            var cityGroups = orders
                .GroupBy(o => string.IsNullOrWhiteSpace(o.DeliveryCity) ? RevenueReportStorage.CityEmptyFilterKey : o.DeliveryCity.Trim())
                .Select(g => new RevenueReportSliceDto
                {
                    Key = g.Key,
                    Name = g.Key == RevenueReportStorage.CityEmptyFilterKey ? "" : g.Key,
                    Income = Round2(g.Sum(o => ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o))),
                    OrderCount = g.Count(),
                })
                .OrderByDescending(s => s.Income)
                .ToList();
            ApplyPct(cityGroups, netTotal);

            var catRevenue = new Dictionary<int, (string name, decimal income, int count)>();
            foreach (var o in orders)
            {
                var share = ChargedAmount(o) - CreditAmount(o) - CancellationAmount(o);
                if (share <= 0) continue;
                foreach (var line in o.OrderItem)
                {
                    if (line.ProductId is not > 0 || !products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (cid == null) continue;
                    if (categoryFilter.Count > 0 && !categoryFilter.Contains(cid.Value)) continue;
                    var merch = LineMerch(line);
                    var totalMerch = o.OrderItem.Sum(LineMerch);
                    if (totalMerch <= 0) continue;
                    var part = share * (merch / totalMerch);
                    var catName = p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? "";
                    if (!catRevenue.ContainsKey(cid.Value))
                        catRevenue[cid.Value] = (catName, 0m, 0);
                    var cur = catRevenue[cid.Value];
                    catRevenue[cid.Value] = (catName, cur.income + part, cur.count + 1);
                }
            }

            var catSorted = catRevenue
                .Select(kv => new RevenueReportSliceDto
                {
                    Key = kv.Key.ToString(),
                    Name = kv.Value.name,
                    Income = Round2(kv.Value.income),
                    OrderCount = kv.Value.count,
                })
                .OrderByDescending(s => s.Income)
                .ToList();
            ApplyPct(catSorted, netTotal);

            return new RevenueReportSegmentsDto
            {
                PaymentSlices = paymentGroups,
                ChannelSlices = channelGroups,
                CitySlices = new RevenueReportCitySlicesDto
                {
                    Top = cityGroups.Take(5).ToList(),
                    MoreCount = Math.Max(0, cityGroups.Count - 5),
                },
                CategorySlices = catSorted,
                CategoryMoreCount = Math.Max(0, catSorted.Count - 5),
            };
        }

        private static void ApplyPct(List<RevenueReportSliceDto> slices, decimal total)
        {
            foreach (var s in slices)
                s.Pct = total > 0 ? Math.Round(100m * s.Income / total, 1) : 0m;
        }

        private static int? PrimaryCategoryId(Product p)
        {
            if (p.ProductCategory == null || p.ProductCategory.Count == 0) return null;
            var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
            return primary?.CategoryId ?? p.ProductCategory.First().CategoryId;
        }

        private static decimal LineMerch(OrderItem i)
        {
            if (i.PickedQuantity is > 0m)
                return i.TotalPrice ?? i.PickedQuantity.Value * (i.PricePerUnit ?? 0m);
            return i.TotalPrice ?? i.Quantity * (i.PricePerUnit ?? 0m);
        }

        private static List<RevenueReportFilterOptionDto> BuildCityOptions(List<Order> orders)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasEmpty = false;
            foreach (var o in orders)
            {
                var c = o.DeliveryCity?.Trim();
                if (string.IsNullOrEmpty(c)) hasEmpty = true;
                else keys.Add(c);
            }

            var list = keys.OrderBy(k => k).Select(k => new RevenueReportFilterOptionDto { Key = k, Name = k }).ToList();
            if (hasEmpty)
                list.Insert(0, new RevenueReportFilterOptionDto { Key = RevenueReportStorage.CityEmptyFilterKey, Name = "" });
            return list;
        }

        private static List<RevenueReportFilterOptionDto> BuildChannelOptions(List<Order> orders) =>
            orders.Select(o => MapChannelKey(o)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(k => new RevenueReportFilterOptionDto { Key = k, Name = k })
                .ToList();

        private static List<RevenueReportFilterOptionDto> BuildPaymentOptions() =>
            new[]
            {
                ("credit", "credit"), ("cash", "cash"), ("bit", "bit"), ("transfer", "transfer"),
            }.Select(x => new RevenueReportFilterOptionDto { Key = x.Item1, Name = x.Item2 }).ToList();

        private static List<RevenueReportFilterOptionDto> BuildStatusOptions() =>
            new[] { "delivered", "credited", "cancelled", "pending" }
                .Select(s => new RevenueReportFilterOptionDto { Key = s, Name = s })
                .ToList();

        private static string BucketKey(DateTime dt, string grouping)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                IsraelTimeZone);
            return grouping switch
            {
                "weekly" => $"{local.Year}-W{ISOWeek.GetWeekOfYear(local):D2}",
                "monthly" => $"{local.Year}-{local.Month:D2}",
                _ => local.ToString("yyyy-MM-dd"),
            };
        }

        private static string BucketLabel(DateTime dt, string grouping)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                IsraelTimeZone);
            return grouping switch
            {
                "weekly" => $"שבוע {ISOWeek.GetWeekOfYear(local)}",
                "monthly" => local.ToString("MM/yyyy"),
                _ => local.ToString("dd/MM"),
            };
        }

        private static IEnumerable<(string key, string date, string label)> EnumerateBuckets(
            DateTime fromUtc, DateTime toUtcExclusive, string grouping)
        {
            var cursor = fromUtc.Date;
            var end = toUtcExclusive.Date;
            while (cursor < end)
            {
                var key = BucketKey(cursor, grouping);
                var label = BucketLabel(cursor, grouping);
                var date = grouping == "daily" ? cursor.ToString("yyyy-MM-dd") : key;
                yield return (key, date, label);
                cursor = grouping switch
                {
                    "weekly" => cursor.AddDays(7),
                    "monthly" => cursor.AddMonths(1),
                    _ => cursor.AddDays(1),
                };
            }
        }

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    }
}
