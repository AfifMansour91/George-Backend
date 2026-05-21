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

        private static HashSet<int> ParseExcludeCategoryIds(string? raw)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(raw)) return set;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id) &&
                    id > 0)
                    set.Add(id);
            }
            return set;
        }

        /// <summary>החרגה אם אחת הקטגוריות המקושרות למוצר מופיעה ברשימת החרגה (הורה או צאצא).</summary>
        private static bool ExcludeProduct(Product p, HashSet<int> exclude)
        {
            if (exclude.Count == 0) return false;
            foreach (var link in p.ProductCategory ?? Enumerable.Empty<ProductCategory>())
            {
                if (exclude.Contains(link.CategoryId))
                    return true;
            }

            return false;
        }

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
            string? excludeCategoryIds,
            string? cutLabel,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<ProductsReportRes>();
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

            var excludeIds = ParseExcludeCategoryIds(excludeCategoryIds);
            var cutFilter = string.IsNullOrWhiteSpace(cutLabel) ? null : cutLabel.Trim();

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

            var wooToProductId = catalogProducts
                .Where(p => p.WooCommerceId is > 0)
                .GroupBy(p => p.WooCommerceId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var lastSaleUtcByProduct = await _incomeReportStorage
                .GetLastPaidCompletedOrderCreationTimeUtcPerProductAsync(
                    siteId,
                    catalogProducts.Select(p => (p.Id, p.WooCommerceId)),
                    cancelToken)
                .ConfigureAwait(false);
            MergeLastSaleTimesFromOrders(lastSaleUtcByProduct, currentOrders, wooToProductId);
            MergeLastSaleTimesFromOrders(lastSaleUtcByProduct, baselineOrders, wooToProductId);

            var soldIdsInPeriod = ComputeSoldProductIdsForPeriod(currentOrders, productDict, catFilter, excludeIds);
            res.Kpis = BuildKpis(
                currentOrders,
                catalogProducts,
                account,
                productDict,
                catFilter,
                soldIdsInPeriod,
                excludeIds,
                lastSaleUtcByProduct,
                utcNow);
            res.CutOptions = BuildCutOptions(currentOrders, productDict, catFilter, excludeIds);

            res.UnsoldProducts = BuildUnsoldProductRows(
                catalogProducts,
                catFilter,
                account,
                soldIdsInPeriod,
                lastSaleUtcByProduct,
                utcNow);

            res.ProductRows = BuildProductRows(currentOrders, baselineOrders, productDict, catFilter, account, excludeIds, cutFilter);
            res.CategorySlices = BuildCategorySlices(currentOrders, productDict, catFilter, excludeIds, categories.Items);
            res.TopOptions = BuildTopOptions(currentOrders, productDict, catFilter, excludeIds);
            res.UpsellPairs = BuildUpsellPairs(currentOrders, productDict, catFilter, excludeIds);

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
            int? categoryId,
            HashSet<int> excludeCategoryIds)
        {
            var soldIds = new HashSet<int>();
            foreach (var o in currentOrders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
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
                    IsWeighted = ProductCatalogStockClassification.IsWeightedLikeProduct(p),
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
            HashSet<int> soldIdsInPeriod,
            HashSet<int> excludeCategoryIds,
            Dictionary<int, DateTime> lastSaleUtcByProduct,
            DateTime utcNow)
        {
            var catRevenue = new Dictionary<int, (string name, decimal rev)>();
            decimal totalRev = 0m;
            foreach (var o in currentOrders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
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
                DaysSinceLastSaleAmongOutOfStock = MinDaysSinceLastSaleAmongStockBucket(
                    catalogProducts, account, "out", lastSaleUtcByProduct, utcNow),
                DaysSinceLastSaleAmongLowStock = MinDaysSinceLastSaleAmongStockBucket(
                    catalogProducts, account, "low", lastSaleUtcByProduct, utcNow),
            };
        }

        private static void MergeLastSaleTimesFromOrders(
            Dictionary<int, DateTime> map,
            IEnumerable<Order> orders,
            IReadOnlyDictionary<int, int>? wooCommerceProductIdToProductId = null)
        {
            foreach (var o in orders)
            {
                var ct = o.CreationTime;
                if (ct.Kind == DateTimeKind.Local)
                    ct = ct.ToUniversalTime();
                else if (ct.Kind == DateTimeKind.Unspecified)
                    ct = DateTime.SpecifyKind(ct, DateTimeKind.Utc);

                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    var pid = line.ProductId;
                    if (pid is not > 0
                        && line.WooCommerceProductId is > 0
                        && wooCommerceProductIdToProductId != null
                        && wooCommerceProductIdToProductId.TryGetValue(line.WooCommerceProductId.Value, out var mapped))
                        pid = mapped;
                    if (pid is not > 0) continue;
                    if (!map.TryGetValue(pid.Value, out var prev) || ct > prev)
                        map[pid.Value] = ct;
                }
            }
        }

        private static int? MinDaysSinceLastSaleAmongStockBucket(
            List<Product> catalogProducts,
            Account? account,
            string stockBucket,
            Dictionary<int, DateTime> lastSaleUtcByProduct,
            DateTime utcNow)
        {
            int? minDays = null;
            foreach (var p in catalogProducts)
            {
                var st = ProductCatalogStockClassification.ClassifyStock(p, account);
                if (!string.Equals(st, stockBucket, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!lastSaleUtcByProduct.TryGetValue(p.Id, out var lastUtc))
                    continue;
                var d = (int)Math.Floor((utcNow.Date - lastUtc.Date).TotalDays);
                d = Math.Max(0, d);
                if (minDays == null || d < minDays)
                    minDays = d;
            }

            return minDays;
        }

        private static List<string> BuildCutOptions(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId,
            HashSet<int> excludeCategoryIds)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    if (LineMerchandise(line) <= 0m) continue;
                    var label = ResolveProductsReportCutLabel(line, p.Name);
                    if (!string.IsNullOrWhiteSpace(label))
                        set.Add(label.Trim());
                }
            }

            return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<ProductsReportProductRowDto> BuildProductRows(
            List<Order> current,
            List<Order> baseline,
            Dictionary<int, Product> products,
            int? categoryId,
            Account? account,
            HashSet<int> excludeCategoryIds,
            string? cutLabelFilter)
        {
            var curAgg = AggregateByProduct(current, products, categoryId, excludeCategoryIds, cutLabelFilter);
            var basAgg = AggregateByProduct(baseline, products, categoryId, excludeCategoryIds, cutLabelFilter);

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
            int? categoryId,
            HashSet<int> excludeCategoryIds,
            string? cutLabelFilter)
        {
            var map = new Dictionary<int, Agg>();
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    var merch = LineMerchandise(line);
                    if (merch <= 0m) continue;

                    var cutLabel = ResolveProductsReportCutLabel(line, p.Name);
                    if (cutLabelFilter != null)
                    {
                        if (cutLabel == null ||
                            !string.Equals(cutLabel, cutLabelFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    if (!map.ContainsKey(p.Id))
                        map[p.Id] = new Agg { name = p.Name, imageUrl = FirstImageUrl(p) };
                    var a = map[p.Id];
                    var (kgLine, unitLine) = SplitLineQty(line, p);
                    a.revenue += merch;
                    a.kg += kgLine;
                    a.units += unitLine;
                    if (!string.IsNullOrEmpty(cutLabel))
                    {
                        if (!a.byCut.TryGetValue(cutLabel, out var c))
                            c = (0m, 0m, 0m);
                        c.revenue += merch;
                        c.kg += kgLine;
                        c.units += unitLine;
                        a.byCut[cutLabel] = c;
                    }

                    map[p.Id] = a;
                }
            }

            return map;
        }

        private static string? ResolveProductsReportCutLabel(OrderItem line, string? productName) =>
            OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, productName);

        private static (decimal kg, decimal units) SplitLineQty(OrderItem line, Product p)
        {
            var mode = (line.OrderLineQuantityMode ?? "").Trim().ToLowerInvariant();
            if (mode == "weight")
            {
                var weightKg = LineWeightKg(line, p);
                return (weightKg ?? 0m, line.LineUnit ?? 0m);
            }

            // Piece-count and legacy weighted lines — same kg/units rules as דוח ריכוז כמויות.
            var (kgLine, unitsFromQc) = QuantityConcentrationReportService.SplitLineQty(line, p);
            var units = unitsFromQc > 0m ? EffectiveLineUnits(line) : 0m;
            return (kgLine, units);
        }

        private static decimal EffectiveLineUnits(OrderItem line)
        {
            if (line.PickingUserConfirmed && line.PickedQuantity is > 0m &&
                !string.Equals(line.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
                return line.PickedQuantity.Value;
            return line.LineUnit ?? line.Quantity;
        }

        private static decimal? LineWeightKg(OrderItem i, Product? p)
        {
            if (i.PickingUserConfirmed && i.PickedQuantity is > 0m &&
                string.Equals(i.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
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

        private static bool IsAncestorOrSelf(int ancestorId, int categoryId, Dictionary<int, int?> parentMap)
        {
            var cur = categoryId;
            for (var guard = 0; guard < 64; guard++)
            {
                if (cur == ancestorId) return true;
                if (!parentMap.TryGetValue(cur, out var p) || p == null) return false;
                cur = p.Value;
            }

            return false;
        }

        private static decimal RollupRevenueForSubtree(int ancestorId, Dictionary<int, decimal> revByCategory, Dictionary<int, int?> parentMap)
        {
            decimal s = 0m;
            foreach (var kv in revByCategory)
            {
                if (IsAncestorOrSelf(ancestorId, kv.Key, parentMap))
                    s += kv.Value;
            }

            return s;
        }

        /// <summary>קטגוריות מינימליות בסט המסומן (ללא אב שיש לו צאצא גם מסומן).</summary>
        private static HashSet<int> MinimalAssignedCategoryNodes(HashSet<int> idSet, Dictionary<int, int?> parentMap)
        {
            var minimal = new HashSet<int>();
            foreach (var x in idSet)
            {
                var hasDescendantAlsoAssigned = idSet.Any(y => y != x && IsAncestorOrSelf(x, y, parentMap));
                if (!hasDescendantAlsoAssigned)
                    minimal.Add(x);
            }

            return minimal;
        }

        /// <summary>שרשרת מהשורש לצאצא — רק צמתים שמופיעים במוצר; משקלול 1..n נותן עדיפות לקטגוריה הספציפית.</summary>
        private static List<int> AssignedAncestorChainRootToLeaf(int deepestAssigned, HashSet<int> idSet, Dictionary<int, int?> parentMap)
        {
            var path = new List<int>();
            var cur = deepestAssigned;
            while (true)
            {
                if (idSet.Contains(cur))
                    path.Add(cur);
                if (!parentMap.TryGetValue(cur, out var p) || p == null)
                    break;
                cur = p.Value;
            }

            path.Reverse();
            return path;
        }

        private static void AddCategoryRevenueSplit(
            Dictionary<int, decimal> revByCat,
            decimal m,
            HashSet<int> idSet,
            Dictionary<int, int?> parentMap)
        {
            if (idSet.Count == 0 || m <= 0m)
                return;

            var minimal = MinimalAssignedCategoryNodes(idSet, parentMap);
            if (minimal.Count == 0)
                return;

            if (minimal.Count == 1)
            {
                var only = minimal.First();
                var chain = AssignedAncestorChainRootToLeaf(only, idSet, parentMap);
                if (chain.Count == 0)
                    return;
                var n = chain.Count;
                var denom = n * (n + 1) / 2m;
                for (var i = 0; i < n; i++)
                {
                    var cid = chain[i];
                    var w = (i + 1) / denom;
                    revByCat[cid] = revByCat.GetValueOrDefault(cid) + m * w;
                }

                return;
            }

            var share = m / minimal.Count;
            foreach (var cid in minimal)
                revByCat[cid] = revByCat.GetValueOrDefault(cid) + share;
        }

        private static string ResolveCategoryDisplayName(int categoryId, Dictionary<int, string> nameMap, Dictionary<int, Product> products)
        {
            if (nameMap.TryGetValue(categoryId, out var n) && !string.IsNullOrWhiteSpace(n))
                return n;
            foreach (var p in products.Values)
            {
                var link = p.ProductCategory?.FirstOrDefault(pc => pc.CategoryId == categoryId);
                if (link?.Category?.Name is { Length: > 0 } nm)
                    return nm;
            }

            return $"#{categoryId}";
        }

        private static List<ProductsReportCategorySliceDto> BuildCategorySlices(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId,
            HashSet<int> excludeCategoryIds,
            List<Category> allCategories)
        {
            var parentMap = allCategories.ToDictionary(c => c.Id, c => c.ParentCategoryId);
            var nameMap = allCategories.ToDictionary(c => c.Id, c => c.Name ?? "");

            var revByCat = new Dictionary<int, decimal>();
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;
                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;

                    var idSet = (p.ProductCategory ?? Enumerable.Empty<ProductCategory>()).Select(x => x.CategoryId).ToHashSet();
                    if (idSet.Count == 0) continue;
                    AddCategoryRevenueSplit(revByCat, m, idSet, parentMap);
                }
            }

            if (revByCat.Count == 0 || revByCat.Values.Sum() <= 0m)
                return new List<ProductsReportCategorySliceDto>();

            var total = revByCat.Values.Sum();
            var ordered = revByCat.OrderByDescending(kv => kv.Value).ToList();
            var list = new List<ProductsReportCategorySliceDto>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var (cid, rev) = ordered[i];
                list.Add(new ProductsReportCategorySliceDto
                {
                    CategoryId = cid,
                    Name = ResolveCategoryDisplayName(cid, nameMap, products),
                    Color = SliceColors[i % SliceColors.Length],
                    Pct = Math.Round(rev / total * 100m, 1, MidpointRounding.AwayFromZero),
                    Revenue = Round2(rev),
                });
            }

            var childrenByParent = parentMap
                .Where(kv => kv.Value != null)
                .GroupBy(kv => kv.Value!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

            foreach (var slice in list)
            {
                if (slice.CategoryId is not int pid) continue;
                if (!childrenByParent.TryGetValue(pid, out var children) || children.Count == 0) continue;

                var parentTotal = revByCat.GetValueOrDefault(pid);
                var rolls = children.ToDictionary(c => c, c => RollupRevenueForSubtree(c, revByCat, parentMap));
                var sumR = rolls.Values.Sum();

                var alloc = new Dictionary<int, decimal>();
                if (sumR <= 0m && parentTotal > 0m)
                {
                    var eq = parentTotal / children.Count;
                    foreach (var c in children)
                        alloc[c] = eq;
                }
                else if (sumR > 0m)
                {
                    foreach (var c in children)
                        alloc[c] = rolls[c] + parentTotal * (rolls[c] / sumR);
                }
                else
                {
                    foreach (var c in children)
                        alloc[c] = rolls[c];
                }

                var sub = new List<ProductsReportCategorySliceDto>();
                foreach (var c in children.OrderByDescending(c => alloc.GetValueOrDefault(c)))
                {
                    var rev = Round2(alloc.GetValueOrDefault(c));
                    if (rev <= 0m) continue;
                    sub.Add(new ProductsReportCategorySliceDto
                    {
                        CategoryId = c,
                        Name = ResolveCategoryDisplayName(c, nameMap, products),
                        Revenue = rev,
                        Pct = 0m,
                    });
                }

                if (sub.Count == 0) continue;

                var baseRev = sub.Sum(s => s.Revenue);
                foreach (var s in sub)
                    s.Pct = baseRev > 0m ? Math.Round(s.Revenue / baseRev * 100m, 1, MidpointRounding.AwayFromZero) : 0m;
                slice.SubSlices = sub;
            }

            return list;
        }

        private static List<ProductsReportOptionRankDto> BuildTopOptions(
            List<Order> orders,
            Dictionary<int, Product> products,
            int? categoryId,
            HashSet<int> excludeCategoryIds)
        {
            var map = new Dictionary<string, (decimal rev, decimal kg, decimal units)>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!products.TryGetValue(line.ProductId.Value, out var p)) continue;
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
                    if (categoryId != null && PrimaryCategoryId(p) != categoryId) continue;

                    var label = ResolveProductsReportCutLabel(line, p.Name);
                    if (string.IsNullOrWhiteSpace(label)) continue;

                    var m = LineMerchandise(line);
                    if (m <= 0m) continue;
                    var (kg, u) = SplitLineQty(line, p);
                    if (!map.TryGetValue(label, out var t2))
                        t2 = (0m, 0m, 0m);
                    map[label] = (t2.rev + m, t2.kg + kg, t2.units + u);
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
            int? categoryId,
            HashSet<int> excludeCategoryIds)
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
                    if (ExcludeProduct(p, excludeCategoryIds)) continue;
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
