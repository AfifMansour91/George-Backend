using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    public class ProductsReportService : ServiceBase
    {
        private readonly IncomeReportStorage _incomeReportStorage;
        private readonly ProductsReportStorage _productsReportStorage;
        private readonly CategoryStorage _categoryStorage;

        private static readonly string[] SliceColors = { "#1E3A5F", "#3B82F6", "#93C5FD", "#F59E0B", "#10B981", "#9CA3AF" };

        public ProductsReportService(
            ILogger<ProductsReportService> logger,
            IMapper mapper,
            CacheManager cache,
            IncomeReportStorage incomeReportStorage,
            ProductsReportStorage productsReportStorage,
            CategoryStorage categoryStorage)
            : base(logger, mapper, cache)
        {
            _incomeReportStorage = incomeReportStorage;
            _productsReportStorage = productsReportStorage;
            _categoryStorage = categoryStorage;
        }

        public async Task<IApiResponse<ProductsReportRes>> GetReportAsync(
            int siteId,
            string period,
            DateTime? customFrom,
            DateTime? customTo,
            int? categoryId,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<ProductsReportRes>();
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

            var len = toUtcExclusive - fromUtc;
            var baselineFrom = fromUtc - len;
            var baselineToEx = fromUtc;

            var currentOrders = await _incomeReportStorage
                .GetReportOrdersAsync(siteId, fromUtc, toUtcExclusive, null, cancelToken)
                .ConfigureAwait(false);
            var baselineOrders = await _incomeReportStorage
                .GetReportOrdersAsync(siteId, baselineFrom, baselineToEx, null, cancelToken)
                .ConfigureAwait(false);

            var productIds = currentOrders.SelectMany(o => o.OrderItem ?? Enumerable.Empty<OrderItem>())
                .Concat(baselineOrders.SelectMany(o => o.OrderItem ?? Enumerable.Empty<OrderItem>()))
                .Select(i => i.ProductId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var productsFromOrders = await _incomeReportStorage
                .GetProductsWithCategoriesAsync(productIds, cancelToken)
                .ConfigureAwait(false);

            var catalogProducts = await _productsReportStorage
                .GetSiteCatalogProductsAsync(siteId, cancelToken)
                .ConfigureAwait(false);

            var account = await _productsReportStorage.GetAccountForSiteAsync(siteId, cancelToken).ConfigureAwait(false);

            var productDict = catalogProducts.ToDictionary(p => p.Id);
            foreach (var kv in productsFromOrders)
            {
                if (!productDict.ContainsKey(kv.Key))
                    productDict[kv.Key] = kv.Value;
            }

            var catFilter = categoryId is > 0 ? categoryId : null;

            var categories = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId, IsEnabled = true },
                new PagingExDto(10_000) { IncludeTotal = false, Skip = 0 },
                cancelToken).ConfigureAwait(false);

            var res = new ProductsReportRes
            {
                CurrentRange = new ProductsReportRangeDto { FromUtc = fromUtc, ToUtcExclusive = toUtcExclusive },
                Categories = categories.Items
                    .Where(c => !c.IsDeleted && c.IsActive)
                    .Select(c => new ProductsReportCategoryOptionDto { Id = c.Id, Name = c.Name })
                    .OrderBy(c => c.Name)
                    .ToList(),
            };

            var soldIdsInPeriod = ComputeSoldProductIdsForPeriod(currentOrders, productDict, catFilter);
            res.Kpis = BuildKpis(currentOrders, catalogProducts, account, productDict, catFilter, soldIdsInPeriod);

            var lastSaleUtcByProduct = await _incomeReportStorage
                .GetLastPaidCompletedOrderCreationTimeUtcPerProductAsync(
                    siteId,
                    catalogProducts.Select(p => p.Id),
                    cancelToken)
                .ConfigureAwait(false);
            res.UnsoldProducts = BuildUnsoldProductRows(
                catalogProducts,
                catFilter,
                account,
                soldIdsInPeriod,
                lastSaleUtcByProduct,
                utcNow);

            res.ProductRows = BuildProductRows(currentOrders, baselineOrders, productDict, catFilter, account);
            res.CategorySlices = BuildCategorySlices(currentOrders, productDict, catFilter);
            res.TopOptions = BuildTopOptions(currentOrders, productDict, catFilter);
            res.UpsellPairs = BuildUpsellPairs(currentOrders, productDict, catFilter);

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

        private const int MaxUnsoldProductRows = 500;

        private static HashSet<int> ComputeSoldProductIdsForPeriod(
            List<Order> currentOrders,
            Dictionary<int, Product> productDict,
            int? categoryId)
        {
            var soldIds = new HashSet<int>();
            foreach (var o in currentOrders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    if (LineMerchandise(line) <= 0m) continue;
                    soldIds.Add(line.ProductId.Value);
                }
            }

            return soldIds;
        }

        private static List<ProductsReportUnsoldRowDto> BuildUnsoldProductRows(
            List<Product> catalogProducts,
            int? categoryId,
            Account? account,
            HashSet<int> soldIdsInPeriod,
            Dictionary<int, DateTime> lastSaleUtcByProduct,
            DateTime utcNow)
        {
            var list = new List<ProductsReportUnsoldRowDto>();
            foreach (var p in catalogProducts)
            {
                if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                if (soldIdsInPeriod.Contains(p.Id)) continue;

                var cid = PrimaryCategoryId(p);
                var catName = cid != null
                    ? p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? ""
                    : "";

                int? daysSinceLastSale = null;
                if (lastSaleUtcByProduct.TryGetValue(p.Id, out var lastUtc))
                {
                    var d = (int)Math.Floor((utcNow.Date - lastUtc.Date).TotalDays);
                    daysSinceLastSale = Math.Max(0, d);
                }

                var st = ProductCatalogStockClassification.ClassifyStock(p, account);
                list.Add(new ProductsReportUnsoldRowDto
                {
                    ProductId = p.Id,
                    Name = p.Name ?? "",
                    CategoryName = catName,
                    DaysSinceLastSale = daysSinceLastSale,
                    StockStatus = st,
                    StockQuantity = p.StockQuantity,
                    IsWeighted = p.IsWeighted == true,
                });
            }

            return list
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxUnsoldProductRows)
                .ToList();
        }

        private static ProductsReportKpisDto BuildKpis(
            List<Order> currentOrders,
            List<Product> catalogProducts,
            Account? account,
            Dictionary<int, Product> productDict,
            int? categoryId,
            HashSet<int> soldIdsInPeriod)
        {
            var catRevenue = new Dictionary<int, (string name, decimal rev)>();
            decimal totalRev = 0m;
            foreach (var o in currentOrders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (categoryId != null && cid != categoryId) continue;
                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;
                    totalRev += m;
                    if (cid == null) continue;
                    var catName = p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? "";
                    if (!catRevenue.ContainsKey(cid.Value))
                        catRevenue[cid.Value] = (catName, 0m);
                    var t = catRevenue[cid.Value];
                    catRevenue[cid.Value] = (t.name, t.rev + m);
                }
            }

            string? leadName = null;
            decimal? leadPct = null;
            if (catRevenue.Count > 0 && totalRev > 0m)
            {
                var top = catRevenue.OrderByDescending(kv => kv.Value.rev).First();
                leadName = top.Value.name;
                leadPct = Math.Round(top.Value.rev / totalRev * 100m, 1, MidpointRounding.AwayFromZero);
            }

            var outCount = 0;
            var lowCount = 0;
            foreach (var p in catalogProducts)
            {
                var st = ProductCatalogStockClassification.ClassifyStock(p, account);
                if (st == "out") outCount++;
                else if (st == "low") lowCount++;
            }

            var unsoldInPeriod = catalogProducts.Count(p =>
                (categoryId == null || PrimaryCategoryId(p) == categoryId) && !soldIdsInPeriod.Contains(p.Id));

            return new ProductsReportKpisDto
            {
                DistinctProductsSold = soldIdsInPeriod.Count,
                CatalogProductCount = catalogProducts.Count,
                UnsoldInPeriodCount = unsoldInPeriod,
                LeadingCategoryName = leadName,
                LeadingCategoryRevenuePct = leadPct,
                OutOfStockCount = outCount,
                LowStockCount = lowCount,
            };
        }

        private static List<ProductsReportProductRowDto> BuildProductRows(
            List<Order> current,
            List<Order> baseline,
            Dictionary<int, Product> products,
            int? categoryId,
            Account? account)
        {
            var curAgg = AggregateByProduct(current, products, categoryId);
            var basAgg = AggregateByProduct(baseline, products, categoryId);

            var rows = curAgg
                .OrderByDescending(kv => kv.Value.revenue)
                .Select(kv =>
                {
                    var pid = kv.Key;
                    var a = kv.Value;
                    basAgg.TryGetValue(pid, out var b);
                    // Agg is a class: when TryGetValue is false, `b` is null — do not dereference.
                    var trendPct = b != null && b.revenue > 0m
                        ? Math.Round((a.revenue - b.revenue) / b.revenue * 100m, 1, MidpointRounding.AwayFromZero)
                        : (decimal?)null;
                    var trendUp = b != null && b.revenue > 0m ? a.revenue >= b.revenue : (bool?)null;

                    var cuts = a.byCut
                        .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                        .OrderByDescending(x => x.Value.revenue)
                        .Select(x => new ProductsReportCutRowDto
                        {
                            CutLabel = x.Key.Trim(),
                            QuantityKg = x.Value.kg > 0m ? Round2(x.Value.kg) : null,
                            QuantityUnits = x.Value.units > 0m ? Round2(x.Value.units) : null,
                            Revenue = Round2(x.Value.revenue),
                        })
                        .ToList();

                    products.TryGetValue(pid, out var p);
                    var catId = p != null ? PrimaryCategoryId(p) : null;
                    var catName = p != null && catId != null
                        ? p.ProductCategory?.FirstOrDefault(x => x.CategoryId == catId)?.Category?.Name ?? ""
                        : "";

                    return new ProductsReportProductRowDto
                    {
                        ProductId = pid,
                        Name = a.name,
                        CategoryName = catName,
                        CategoryId = catId,
                        ImageUrl = a.imageUrl,
                        QuantityKg = a.kg > 0m ? Round2(a.kg) : null,
                        QuantityUnits = a.units > 0m ? Round2(a.units) : null,
                        Revenue = Round2(a.revenue),
                        TrendPct = trendPct,
                        TrendUp = trendUp,
                        StockStatus = p != null ? ProductCatalogStockClassification.ClassifyStock(p, account) : "ok",
                        CutRows = cuts,
                    };
                })
                .ToList();

            return rows;
        }

        private sealed class Agg
        {
            public string name = "";
            public string? imageUrl;
            public decimal revenue;
            public decimal kg;
            public decimal units;
            public readonly Dictionary<string, (decimal revenue, decimal kg, decimal units)> byCut = new(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<int, Agg> AggregateByProduct(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var map = new Dictionary<int, Agg>();
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    var merch = LineMerchandise(line);
                    if (merch <= 0m) continue;

                    if (!map.ContainsKey(p.Id))
                        map[p.Id] = new Agg { name = p.Name, imageUrl = FirstImageUrl(p) };
                    var a = map[p.Id];
                    var (kgLine, unitLine) = SplitLineQty(line, p);
                    a.revenue += merch;
                    a.kg += kgLine;
                    a.units += unitLine;
                    var cutKey = (line.OrderLineCuttingLabel ?? "").Trim();
                    if (!string.IsNullOrEmpty(cutKey))
                    {
                        if (!a.byCut.TryGetValue(cutKey, out var c))
                            c = (0m, 0m, 0m);
                        c.revenue += merch;
                        c.kg += kgLine;
                        c.units += unitLine;
                        a.byCut[cutKey] = c;
                    }

                    map[p.Id] = a;
                }
            }

            return map;
        }

        private static (decimal kg, decimal units) SplitLineQty(OrderItem line, Product p)
        {
            var mode = (line.OrderLineQuantityMode ?? "").Trim().ToLowerInvariant();
            if (mode == "weight")
            {
                var kg = LineWeightKg(line);
                return (kg ?? 0m, line.LineUnit ?? 0m);
            }

            if (mode == "units")
            {
                var u = line.LineUnit ?? line.Quantity;
                return (0m, u);
            }

            if (p.IsWeighted == true)
            {
                var kg = LineWeightKg(line);
                if (kg is > 0m)
                    return (kg.Value, line.LineUnit ?? 0m);
            }

            return (LineWeightKg(line) ?? 0m, line.LineUnit ?? line.Quantity);
        }

        private static decimal? LineWeightKg(OrderItem i)
        {
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

        private static List<ProductsReportCategorySliceDto> BuildCategorySlices(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var byCat = new Dictionary<int, (string name, decimal rev)>();
            decimal total = 0m;
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (cid == null) continue;
                    if (categoryId != null && cid != categoryId) continue;
                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;
                    total += m;
                    var name = p.ProductCategory?.FirstOrDefault(x => x.CategoryId == cid)?.Category?.Name ?? "";
                    if (!byCat.ContainsKey(cid.Value))
                        byCat[cid.Value] = (name, 0m);
                    var t = byCat[cid.Value];
                    byCat[cid.Value] = (t.name, t.rev + m);
                }
            }

            if (total <= 0m) return new List<ProductsReportCategorySliceDto>();

            var ordered = byCat.OrderByDescending(kv => kv.Value.rev).ToList();
            var list = new List<ProductsReportCategorySliceDto>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var tuple = ordered[i].Value;
                list.Add(new ProductsReportCategorySliceDto
                {
                    Name = tuple.name,
                    Color = SliceColors[i % SliceColors.Length],
                    Pct = Math.Round(tuple.rev / total * 100m, 1, MidpointRounding.AwayFromZero),
                    Revenue = Round2(tuple.rev),
                });
            }

            return list;
        }

        private static List<ProductsReportOptionRankDto> BuildTopOptions(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var map = new Dictionary<string, (decimal rev, decimal kg, decimal units)>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    var label = (line.OrderLineCuttingLabel ?? "").Trim();
                    if (string.IsNullOrEmpty(label)) continue;
                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;
                    var (kg, u) = SplitLineQty(line, p);
                    if (!map.TryGetValue(label, out var t))
                        t = (0m, 0m, 0m);
                    map[label] = (t.rev + m, t.kg + kg, t.units + u);
                }
            }

            var rank = 1;
            return map
                .OrderByDescending(kv => kv.Value.rev)
                .Take(15)
                .Select(kv =>
                {
                    var qtyLabel = kv.Value.kg >= kv.Value.units && kv.Value.kg > 0m
                        ? $"{Round2(kv.Value.kg)} ק״ג"
                        : kv.Value.units > 0m
                            ? $"{Round2(kv.Value.units)} יח׳"
                            : null;
                    return new ProductsReportOptionRankDto
                    {
                        Rank = rank++,
                        OptionLabel = kv.Key,
                        Revenue = Round2(kv.Value.rev),
                        QuantityLabel = qtyLabel,
                    };
                })
                .ToList();
        }

        private static List<ProductsReportUpsellPairDto> BuildUpsellPairs(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId)
        {
            var orderCount = orders.Count;
            if (orderCount == 0) return new List<ProductsReportUpsellPairDto>();

            var pairOrders = new Dictionary<(int a, int b), HashSet<int>>();
            var pairRevenue = new Dictionary<(int a, int b), decimal>();

            foreach (var o in orders)
            {
                var lineByPid = new Dictionary<int, decimal>();
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;
                    lineByPid[line.ProductId.Value] = lineByPid.GetValueOrDefault(line.ProductId.Value) + m;
                }

                var ids = lineByPid.Keys.OrderBy(x => x).ToList();
                for (var i = 0; i < ids.Count; i++)
                {
                    for (var j = i + 1; j < ids.Count; j++)
                    {
                        var a = ids[i];
                        var b = ids[j];
                        var key = a < b ? (a, b) : (b, a);
                        if (!pairOrders.TryGetValue(key, out var set))
                        {
                            set = new HashSet<int>();
                            pairOrders[key] = set;
                        }

                        set.Add(o.Id);
                        var addRev = lineByPid[a] + lineByPid[b];
                        pairRevenue[key] = pairRevenue.GetValueOrDefault(key) + addRev;
                    }
                }
            }

            var candidates = new List<(int pa, int pb, decimal pct, decimal bundleRev)>();
            foreach (var kv in pairOrders)
            {
                var (pa, pb) = kv.Key;
                var orderSet = kv.Value;
                var pct = Math.Round((decimal)orderSet.Count / orderCount * 100m, 1, MidpointRounding.AwayFromZero);
                if (pct < 30m) continue;
                var bundleRev = pairRevenue.GetValueOrDefault((pa, pb));
                candidates.Add((pa, pb, pct, bundleRev));
            }

            return candidates
                .OrderByDescending(x => x.pct)
                .Take(5)
                .Select(x =>
                {
                    products.TryGetValue(x.pa, out var pa);
                    products.TryGetValue(x.pb, out var pb);
                    return new ProductsReportUpsellPairDto
                    {
                        ProductAId = x.pa,
                        ProductAName = pa?.Name ?? $"#{x.pa}",
                        ProductAImageUrl = pa != null ? FirstImageUrl(pa) : null,
                        ProductBId = x.pb,
                        ProductBName = pb?.Name ?? $"#{x.pb}",
                        ProductBImageUrl = pb != null ? FirstImageUrl(pb) : null,
                        OrdersPct = x.pct,
                        BundleRevenue = x.bundleRev > 0m ? Round2(x.bundleRev) : null,
                    };
                })
                .ToList();
        }

        private static int? PrimaryCategoryId(Product? p)
        {
            if (p?.ProductCategory == null || p.ProductCategory.Count == 0) return null;
            var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
            if (primary != null) return primary.CategoryId;
            return p.ProductCategory.First().CategoryId;
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

        private static string? FirstImageUrl(Product? p)
        {
            if (p?.ProductImage == null || p.ProductImage.Count == 0) return null;
            return p.ProductImage.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault();
        }

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    }
}
