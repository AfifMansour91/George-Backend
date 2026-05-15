using System.Text.Json;
using System.Text.RegularExpressions;
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
        private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

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

        /// <summary>
        /// נקודת זמן UTC כמו ב־API של הזמנות (מחרוזת ISO עם Z) — ל־<see cref="IncomeReportOrderRowDto.OrderDate"/>.
        /// </summary>
        private static DateTime CreationTimeUtcInstant(Order o)
        {
            var ct = o.CreationTime;
            if (ct.Kind == DateTimeKind.Local)
                return TimeZoneInfo.ConvertTimeToUtc(ct);
            return AssumeUtc(ct);
        }

        private static DateTime ToIsraelLocal(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(AssumeUtc(utc), IsraelTimeZone);

        /// <summary>
        /// שעת יצירת הזמנה בשעון ישראל — מיושר ל־SPA (היסטוריית הזמנה משתמשת ב־<c>new Date(creationTime)</c> שמפרש ISO עם Z כ־UTC וממיר לשעון מקומי).
        /// ערך <see cref="DateTimeKind.Unspecified"/> מהמסד מטופל כ־UTC (דפוס נפוץ אחרי EF/SQL).
        /// </summary>
        private static int OrderCreationHourIsrael(Order o)
        {
            var ct = o.CreationTime;
            if (ct.Kind == DateTimeKind.Local)
            {
                var utc = TimeZoneInfo.ConvertTimeToUtc(ct);
                return ToIsraelLocal(utc).Hour;
            }

            return ToIsraelLocal(AssumeUtc(ct)).Hour;
        }

        /// <summary>מחלץ את תחילת חלון האספקה (למשל "11:00" מ־"11:00 - 12:00").</summary>
        private static bool TryParseSlotStartHour(string? deliveryOrPickupTime, out int hour)
        {
            hour = 0;
            if (string.IsNullOrWhiteSpace(deliveryOrPickupTime))
                return false;
            var t = deliveryOrPickupTime.Trim();
            foreach (var sep in new[] { " - ", " – ", " — ", "-", "–", "—" })
            {
                var idx = t.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0)
                {
                    t = t[..idx].Trim();
                    break;
                }
            }

            var m = Regex.Match(t, @"(?<!\d)(\d{1,2}):(\d{2})(?::\d{2})?");
            if (!m.Success)
                m = Regex.Match(deliveryOrPickupTime.Trim(), @"(?<!\d)(\d{1,2}):(\d{2})(?::\d{2})?");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var h))
            {
                if (h is >= 0 and <= 23)
                {
                    hour = h;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// שעת חלון אספקה/איסוף — כמו <see cref="OrderArchiveDetail"/> (איסוף: pickup*; משלוח/אקספרס: delivery*).
        /// עדיפות לתחילת מחרוזת השעה; אחר כך שעה בשדה התאריך אם אינה חצות.
        /// </summary>
        private static bool TryGetScheduledDeliveryHourIsrael(Order o, out int hour)
        {
            hour = 0;
            var isPickup = string.Equals(o.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase);
            var cal = isPickup ? o.PickupDate : o.DeliveryDate;
            var slot = isPickup
                ? (o.PickupTime ?? o.DeliveryTime)
                : (o.DeliveryTime ?? o.PickupTime);
            if (cal == null)
                return false;

            if (TryParseSlotStartHour(slot, out var hSlot))
            {
                hour = hSlot;
                return true;
            }

            var dt = cal.Value;
            if (dt.Hour != 0 || dt.Minute != 0 || dt.Second != 0)
            {
                hour = dt.Kind == DateTimeKind.Utc
                    ? ToIsraelLocal(AssumeUtc(dt)).Hour
                    : dt.Hour;
                return true;
            }

            return false;
        }

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
            res.DayRows = BuildDayRows(currentOrders, products, catFilter, fromUtc, toUtcExclusive, period);
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
                {
                    // שבוע קלנדרי: מיום שני 00:00 עד סוף היום (UTC), כמו שבוע נוכחי חלקי
                    var dow = (int)today.DayOfWeek;
                    var daysFromMonday = dow == (int)DayOfWeek.Sunday ? 6 : dow - (int)DayOfWeek.Monday;
                    var monday = today.AddDays(-daysFromMonday);
                    return (monday, today.AddDays(1));
                }
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

        /// <summary>
        /// מספר שורות פריט (לא סכום כמויות) — לפי כל השורות או רק שורות בקטגוריה הנבחרת.
        /// </summary>
        private static int OrderMerchLineCount(Order o, Dictionary<int, Product> products, int? categoryId)
        {
            var items = o.OrderItem ?? Enumerable.Empty<OrderItem>();
            if (categoryId == null)
                return items.Count(i => !i.IsDeleted && (i.ProductId ?? 0) > 0);
            return items.Count(line =>
            {
                if (line.IsDeleted || !(line.ProductId is > 0)) return false;
                if (!products.TryGetValue(line.ProductId.Value, out var p)) return false;
                return PrimaryCategoryId(p) == categoryId;
            });
        }

        private static IncomeReportKpisDto BuildKpis(
            List<Order> current,
            List<Order> baseline,
            Dictionary<int, Product> products,
            int? categoryId,
            HashSet<int> priorCustomerIds)
        {
            decimal AllocIncome(Order o) => OrderIncome(o) * CategoryIncomeShare(o, products, categoryId);

            var curOrders = current.Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value)).ToList();
            var baseOrders = baseline.Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value)).ToList();

            var totalIncome = curOrders.Sum(AllocIncome);
            var totalUnfiltered = current.Sum(OrderIncome);
            var baseIncome = baseOrders.Sum(AllocIncome);

            var cCount = curOrders.Count;
            var bCount = baseOrders.Count;
            var avgOrder = cCount > 0 ? totalIncome / cCount : 0m;
            var avgBase = bCount > 0 ? baseIncome / bCount : 0m;
            var avgItems = cCount > 0 ? (decimal)curOrders.Sum(o => OrderMerchLineCount(o, products, categoryId)) / cCount : 0m;
            var avgItemsB = bCount > 0 ? (decimal)baseOrders.Sum(o => OrderMerchLineCount(o, products, categoryId)) / bCount : 0m;

            // חדש מול חוזר לפי *עסקה*: העסקה הראשונה של הלקוח (באתר) = חדש; כל עסקה נוספת = חוזר,
            // גם כששתי ההזמנות באותה תקופת דוח (לפני כן נספרו שתיהן כ"חדש" כי לא היה Order לפני fromUtc).
            decimal retInc = 0m, newInc = 0m;
            var newCustomerAttributedInWindow = new HashSet<int>();
            foreach (var o in curOrders.OrderBy(x => x.CreationTime).ThenBy(x => x.Id))
            {
                var inc = AllocIncome(o);
                var returning = false;
                if (o.CustomerId is int cid)
                {
                    if (priorCustomerIds.Contains(cid))
                        returning = true;
                    else if (newCustomerAttributedInWindow.Contains(cid))
                        returning = true;
                    else
                        newCustomerAttributedInWindow.Add(cid);
                }

                if (returning)
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
            DateTime toUtcExclusive,
            string period)
        {
            _ = toUtcExclusive;
            var p = (period ?? "month").Trim().ToLowerInvariant();
            if (p == "today")
                return BuildDayRowsByHour(orders, products, categoryId, fromUtc);

            var dayKeys = orders.Select(o => ToIsraelLocal(o.CreationTime).Date).Distinct().OrderBy(d => d);
            var rows = new List<IncomeReportDayRowDto>();
            foreach (var day in dayKeys)
            {
                var dayOrdersAll = orders.Where(o => ToIsraelLocal(o.CreationTime).Date == day).ToList();
                AppendDayBucketRow(rows, dayOrdersAll, products, categoryId, day.ToString("dd/MM"), day.ToString("yyyy-MM-dd"));
            }

            return rows;
        }

        /// <summary>אפיון: כשפילטר = היום — שורות לפי שעה.</summary>
        private static List<IncomeReportDayRowDto> BuildDayRowsByHour(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId,
            DateTime fromUtc)
        {
            var day = ToIsraelLocal(AssumeUtc(fromUtc)).Date;
            var dayOrders = orders.Where(o => ToIsraelLocal(o.CreationTime).Date == day).ToList();
            var rows = new List<IncomeReportDayRowDto>();
            foreach (var hour in dayOrders.Select(o => ToIsraelLocal(o.CreationTime).Hour).Distinct().OrderBy(h => h))
            {
                var hourOrdersAll = dayOrders.Where(o => ToIsraelLocal(o.CreationTime).Hour == hour).ToList();
                AppendDayBucketRow(rows, hourOrdersAll, products, categoryId, $"{hour:00}:00", $"{day:yyyy-MM-dd}T{hour:00}:00");
            }

            return rows;
        }

        private static void AppendDayBucketRow(
            List<IncomeReportDayRowDto> rows,
            List<Order> bucketOrdersAll,
            Dictionary<int, Product> products,
            int? categoryId,
            string label,
            string dateKey)
        {
            var bucketFiltered = bucketOrdersAll
                .Where(o => categoryId == null || OrderHasCategory(o, products, categoryId.Value))
                .ToList();

            decimal income = 0m, incomeTotal = 0m;
            var orderIds = new HashSet<int>();
            decimal delRev = 0m, pickRev = 0m, ship = 0m, disc = 0m;
            var delOrd = new HashSet<int>();
            var pickOrd = new HashSet<int>();

            foreach (var o in bucketOrdersAll)
                incomeTotal += OrderIncome(o);

            foreach (var o in bucketFiltered)
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
                Date = dateKey,
                Label = label,
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
                    OrderNumber = o.OrderNumber ?? "",
                    OrderDate = CreationTimeUtcInstant(o),
                    CustomerName = o.CustomerName ?? "",
                    Source = o.Source ?? "",
                    ProductRevenue = Round2(OrderLinesMerch(o) * sh),
                    Income = Round2(OrderIncome(o) * sh),
                    ShippingFee = Round2(OrderShipping(o) * sh),
                    Discount = Round2(OrderDiscount(o) * sh),
                    DeliveryType = o.DeliveryType ?? "",
                    CouponCode = TryGetCouponCodeFromOrder(o) ?? "",
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
            var deliveryOrderIds = new HashSet<int>();
            var pickupOrderIds = new HashSet<int>();
            var sourceOrderIds = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var o in relevant)
            {
                var sh = CategoryIncomeShare(o, products, categoryId);
                var inc = OrderIncome(o) * sh;
                var isShip = string.Equals(o.DeliveryType, "Shipping", StringComparison.OrdinalIgnoreCase);
                if (isShip)
                    dInc += inc;
                else
                    pInc += inc;

                if (sh > 0m)
                {
                    if (isShip) deliveryOrderIds.Add(o.Id);
                    else pickupOrderIds.Add(o.Id);
                }

                var sk = MapSourceKey(o.Source);
                if (!bySource.ContainsKey(sk)) bySource[sk] = 0m;
                bySource[sk] += inc;
                if (sh > 0m)
                {
                    if (!sourceOrderIds.TryGetValue(sk, out var set))
                    {
                        set = new HashSet<int>();
                        sourceOrderIds[sk] = set;
                    }
                    set.Add(o.Id);
                }
            }

            var tot = dInc + pInc;
            var dto = new IncomeReportSegmentsDto
            {
                DeliveryPct = tot > 0m ? Round2(dInc / tot * 100m) : 0m,
                PickupPct = tot > 0m ? Round2(pInc / tot * 100m) : 0m,
                DeliveryIncome = Round2(dInc),
                PickupIncome = Round2(pInc),
                DeliveryOrderCount = deliveryOrderIds.Count,
                PickupOrderCount = pickupOrderIds.Count,
            };

            var srcTotal = bySource.Values.Sum();
            if (srcTotal > 0m)
            {
                foreach (var kv in bySource.OrderByDescending(kv => kv.Value))
                {
                    sourceOrderIds.TryGetValue(kv.Key, out var ordSet);
                    dto.SourceSlices.Add(new IncomeReportSourceSliceDto
                    {
                        Key = kv.Key,
                        Name = kv.Key,
                        Pct = Round2(kv.Value / srcTotal * 100m),
                        Income = Round2(kv.Value),
                        OrderCount = ordSet?.Count ?? 0,
                    });
                }
            }

            dto.OrderHours = BuildOrderHourBuckets(relevant);
            dto.DeliveryHours = BuildDeliveryHourBuckets(relevant);

            var topOh = dto.OrderHours.OrderByDescending(x => x.OrderCount).FirstOrDefault();
            if (topOh != null)
                dto.PeakOrderHourLabel = topOh.Label;

            var topDh = dto.DeliveryHours.OrderByDescending(x => x.OrderCount).FirstOrDefault();
            if (topDh != null)
                dto.PeakDeliveryHourLabel = topDh.Label;

            return dto;
        }

        private static List<IncomeReportHourBucketDto> BuildOrderHourBuckets(List<Order> relevant)
        {
            var totalOrders = relevant.Count;
            if (totalOrders == 0)
                return new List<IncomeReportHourBucketDto>();

            return relevant
                .GroupBy(OrderCreationHourIsrael)
                .OrderBy(g => g.Key)
                .Select(g => new IncomeReportHourBucketDto
                {
                    Hour = g.Key,
                    Label = HourRangeLabel(g.Key),
                    OrderCount = g.Count(),
                    PctOfTotal = Round2((decimal)g.Count() / totalOrders * 100m),
                })
                .ToList();
        }

        private static List<IncomeReportHourBucketDto> BuildDeliveryHourBuckets(List<Order> relevant)
        {
            var withHour = relevant.Where(o => TryGetScheduledDeliveryHourIsrael(o, out _)).ToList();
            var total = withHour.Count;
            if (total == 0)
                return new List<IncomeReportHourBucketDto>();

            return withHour
                .GroupBy(o =>
                {
                    TryGetScheduledDeliveryHourIsrael(o, out var h);
                    return h;
                })
                .OrderBy(g => g.Key)
                .Select(g => new IncomeReportHourBucketDto
                {
                    Hour = g.Key,
                    Label = HourRangeLabel(g.Key),
                    OrderCount = g.Count(),
                    PctOfTotal = Round2((decimal)g.Count() / total * 100m),
                })
                .ToList();
        }

        private static string HourRangeLabel(int hour)
        {
            var next = (hour + 1) % 24;
            return $"{hour:00}:00–{next:00}:00";
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
                    var qtyLabel = FormatTopProductQtyLabel(t.kg, t.units, products.TryGetValue(kv.Key, out var pProd) ? pProd : null);
                    return new IncomeReportTopProductDto
                    {
                        ProductId = kv.Key,
                        Name = t.name,
                        CategoryName = t.cat,
                        QuantityLabel = qtyLabel,
                        QuantityKg = t.kg > 0m ? Round2(t.kg) : null,
                        QuantityUnits = t.units > 0m ? Round2(t.units) : null,
                        Revenue = Round2(t.revenue),
                        TrendUp = trendUp,
                        ImageUrl = t.imageUrl,
                    };
                })
                .ToList();
        }

        private sealed class TopAgg
        {
            public string name = "";
            public string cat = "";
            public decimal kg;
            public decimal units;
            public decimal revenue;
            public string? imageUrl;
        }

        private static Dictionary<int, TopAgg> AggregateProductRevenue(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var map = new Dictionary<int, TopAgg>();
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
                    var catName = p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? "";
                    if (!map.ContainsKey(p.Id))
                        map[p.Id] = new TopAgg { name = p.Name ?? "", cat = catName, imageUrl = FirstImageUrl(p) };
                    var t = map[p.Id];
                    var (kgLine, unitLine) = SplitIncomeReportLineQty(line, p);
                    t.revenue += merch;
                    t.kg += kgLine;
                    t.units += unitLine;
                    if (string.IsNullOrEmpty(t.cat) && !string.IsNullOrEmpty(catName))
                        t.cat = catName;
                    map[p.Id] = t;
                }
            }

            return map;
        }

        private static (decimal kg, decimal units) SplitIncomeReportLineQty(OrderItem line, Product p)
        {
            var mode = (line.OrderLineQuantityMode ?? "").Trim().ToLowerInvariant();
            if (mode == "weight")
            {
                var kg = LineWeightKgForReport(line, p);
                return (kg ?? 0m, line.LineUnit ?? 0m);
            }

            if (mode == "units")
            {
                var u = EffectiveLineUnitQuantity(line);
                return (0m, u);
            }

            if (p.IsWeighted == true)
            {
                var kg = LineWeightKgForReport(line, p);
                if (kg is > 0m)
                    return (kg.Value, line.LineUnit ?? 0m);
            }

            return (LineWeightKgForReport(line, p) ?? 0m, EffectiveLineUnitQuantity(line));
        }

        private static decimal EffectiveLineUnitQuantity(OrderItem line)
        {
            if (line.PickingUserConfirmed && line.PickedQuantity is > 0m &&
                !string.Equals(line.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
                return line.PickedQuantity.Value;
            return line.LineUnit ?? line.Quantity;
        }

        private static decimal? LineWeightKgForReport(OrderItem i, Product p)
        {
            if (i.PickingUserConfirmed && i.PickedQuantity is > 0m &&
                string.Equals(i.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
                return i.PickedQuantity.Value;

            if (i.PickingUserConfirmed && i.PickedQuantity is > 0m && p.IsWeighted == true)
                return i.PickedQuantity.Value;

            if (i.PickedQuantity is > 0m &&
                string.Equals(i.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
                return i.PickedQuantity.Value;

            if (i.UnitWeightGrams is > 0m && i.Quantity > 0m)
                return i.Quantity * (i.UnitWeightGrams.Value / 1000m);

            if (!string.IsNullOrWhiteSpace(i.SaleTotalWeight) &&
                decimal.TryParse(i.SaleTotalWeight.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var w))
                return w;

            if (i.LineUnitWeightKg is > 0m && i.Quantity > 0m)
                return i.Quantity * i.LineUnitWeightKg.Value;

            return null;
        }

        private static string FormatTopProductQtyLabel(decimal kg, decimal units, Product? p)
        {
            var weighted = p?.IsWeighted == true;
            if (kg > 0m && (units <= 0m || weighted))
                return $"{Round2(kg)} ק\"ג";
            if (kg > 0m && units > 0m)
            {
                var uStr = units == Math.Floor(units) ? ((int)units).ToString() : Round2(units).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return $"{uStr} יח' · {Round2(kg)} ק\"ג";
            }
            if (units > 0m)
                return units == Math.Floor(units) ? $"{(int)units} יח'" : $"{Round2(units)} יח'";
            return "—";
        }

        private static string? FirstImageUrl(Product? p)
        {
            if (p?.ProductImage == null || p.ProductImage.Count == 0) return null;
            return p.ProductImage.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault();
        }

        private static string? TryGetCouponCodeFromOrder(Order o)
        {
            if (!string.IsNullOrWhiteSpace(o.CouponCode))
                return o.CouponCode.Trim();

            var code = TryParseCouponFromJson(o.WooCommerceRequestJson);
            if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
            code = TryParseCouponFromJson(o.ShippingInfoJson);
            if (!string.IsNullOrWhiteSpace(code)) return code.Trim();

            if (!string.IsNullOrWhiteSpace(o.BillingNotes))
            {
                var m = Regex.Match(o.BillingNotes, @"(?:coupon|קופון)\s*[:\s]+([A-Za-z0-9_-]{2,40})", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }

            return null;
        }

        private static string? TryParseCouponFromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return FindCouponRecursive(doc.RootElement, 0);
            }
            catch
            {
                return null;
            }
        }

        private static string? FindCouponRecursive(JsonElement el, int depth)
        {
            if (depth > 8) return null;

            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in el.EnumerateObject())
                {
                    if (p.Name.Equals("coupon", StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Equals("couponCode", StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Equals("coupon_code", StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.Value.ValueKind == JsonValueKind.String)
                            return p.Value.GetString();
                        if (p.Value.ValueKind == JsonValueKind.Object &&
                            p.Value.TryGetProperty("code", out var codeEl) &&
                            codeEl.ValueKind == JsonValueKind.String)
                            return codeEl.GetString();
                    }

                    var nested = FindCouponRecursive(p.Value, depth + 1);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    var nested = FindCouponRecursive(item, depth + 1);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }

            return null;
        }

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    }
}
