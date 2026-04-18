using System.Globalization;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    /// <summary>דוח ריכוז כמויות — הזמנות פתוחות לפי תאריך אספקה, קיבוץ לפי מוצר+חיתוך+הערה.</summary>
    public class QuantityConcentrationReportService : ServiceBase
    {
        private readonly QuantityConcentrationReportStorage _storage;
        private readonly IncomeReportStorage _incomeReportStorage;
        private readonly ProductsReportStorage _productsReportStorage;
        private readonly CategoryStorage _categoryStorage;

        public QuantityConcentrationReportService(
            ILogger<QuantityConcentrationReportService> logger,
            IMapper mapper,
            CacheManager cache,
            QuantityConcentrationReportStorage storage,
            IncomeReportStorage incomeReportStorage,
            ProductsReportStorage productsReportStorage,
            CategoryStorage categoryStorage)
            : base(logger, mapper, cache)
        {
            _storage = storage;
            _incomeReportStorage = incomeReportStorage;
            _productsReportStorage = productsReportStorage;
            _categoryStorage = categoryStorage;
        }

        public async Task<IApiResponse<QuantityConcentrationReportRes>> GetReportAsync(
            int siteId,
            DateTime? from,
            DateTime? to,
            int? categoryId,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<QuantityConcentrationReportRes>();
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            if (from == null || to == null)
                return CreateResponse(response, StatusCode.InvalidRequest, "from and to are required.");
            if (to.Value.Date < from.Value.Date)
                return CreateResponse(response, StatusCode.InvalidRequest, "Invalid date range.");

            var fromD = from.Value.Date;
            var toD = to.Value.Date;

            var orders = await _storage
                .GetOpenOrdersForDeliveryDateRangeAsync(siteId, fromD, toD, cancelToken)
                .ConfigureAwait(false);

            var catalogProducts = await _productsReportStorage
                .GetSiteCatalogProductsAsync(siteId, cancelToken)
                .ConfigureAwait(false);
            var account = await _productsReportStorage.GetAccountForSiteAsync(siteId, cancelToken).ConfigureAwait(false);

            var productDict = catalogProducts.ToDictionary(p => p.Id);
            var lineProductIds = orders
                .SelectMany(o => o.OrderItem ?? Enumerable.Empty<OrderItem>())
                .Select(i => i.ProductId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var missingIds = lineProductIds.Where(id => !productDict.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                var extra = await _incomeReportStorage
                    .GetProductsWithCategoriesAsync(missingIds, cancelToken)
                    .ConfigureAwait(false);
                foreach (var kv in extra)
                {
                    if (!productDict.ContainsKey(kv.Key))
                        productDict[kv.Key] = kv.Value;
                }
            }

            var catFilter = categoryId is > 0 ? categoryId : null;

            var categories = await _categoryStorage
                .GetCategoriesAsync(
                    new CategoryFilter { SiteId = siteId, IsEnabled = true },
                    new PagingExDto(10_000) { IncludeTotal = false, Skip = 0 },
                    cancelToken)
                .ConfigureAwait(false);

            var res = new QuantityConcentrationReportRes
            {
                DeliveryRange = new QuantityConcentrationRangeDto
                {
                    FromLocal = fromD.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ToLocal = toD.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                },
                Categories = categories.Items
                    .Where(c => !c.IsDeleted && c.IsActive)
                    .Select(c => new QuantityConcentrationCategoryOptionDto { Id = c.Id, Name = c.Name ?? "" })
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };

            // (productId, cutKey, note) -> bucket
            var buckets = new Dictionary<(int pid, string cutKey, string note), LineBucket>(new LineBucketComparer());

            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (catFilter != null && cid != catFilter) continue;
                    if (!LineHasQuantity(line)) continue;

                    var (kg, units) = SplitLineQty(line, p);
                    if (kg <= 0m && units <= 0m) continue;

                    var note = (line.Notes ?? "").Trim();
                    var cutKey = BuildCutBucketKey(line);
                    var key = (line.ProductId.Value, cutKey, note);
                    if (!buckets.TryGetValue(key, out var b))
                    {
                        b = new LineBucket { LineLabel = BuildLineLabel(line) };
                        buckets[key] = b;
                    }

                    b.Kg += kg;
                    b.Units += units;
                    if (b.UnitWeightKg == null && line.LineUnitWeightKg is > 0m)
                        b.UnitWeightKg = line.LineUnitWeightKg;
                    else if (b.UnitWeightKg == null && line.UnitWeightGrams is > 0m)
                        b.UnitWeightKg = line.UnitWeightGrams.Value / 1000m;
                }
            }

            var byProduct = buckets
                .GroupBy(kv => kv.Key.pid)
                .ToList();

            var groups = new List<QuantityConcentrationProductGroupDto>();
            foreach (var g in byProduct.OrderBy(x => productDict.TryGetValue(x.Key, out var pp) ? pp.Name : $"#{x.Key}", StringComparer.OrdinalIgnoreCase))
            {
                if (!productDict.TryGetValue(g.Key, out var p))
                    continue;

                decimal sumKg = 0m, sumUnits = 0m;
                var lines = new List<QuantityConcentrationLineDto>();
                foreach (var kv in g.OrderBy(x => x.Value.LineLabel, StringComparer.OrdinalIgnoreCase))
                {
                    var b = kv.Value;
                    if (b.Kg > 0m) sumKg += b.Kg;
                    if (b.Units > 0m) sumUnits += b.Units;
                    lines.Add(new QuantityConcentrationLineDto
                    {
                        LineLabel = b.LineLabel,
                        WeightPerUnitKg = b.UnitWeightKg is > 0m ? Round2(b.UnitWeightKg.Value) : null,
                        QuantityKg = b.Kg > 0m ? Round2(b.Kg) : null,
                        QuantityUnits = b.Units > 0m ? Round2(b.Units) : null,
                        Note = string.IsNullOrEmpty(kv.Key.note) ? null : kv.Key.note,
                    });
                }

                var (stockKg, stockUnits, shortageKg, shortageUnits, st) = ComputeStockAndShortage(p, account, sumKg, sumUnits);

                groups.Add(new QuantityConcentrationProductGroupDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name ?? "",
                    CategoryId = PrimaryCategoryId(p) ?? 0,
                    TotalQuantityKg = sumKg > 0m ? Round2(sumKg) : null,
                    TotalQuantityUnits = sumUnits > 0m ? Round2(sumUnits) : null,
                    StockKg = stockKg,
                    StockUnits = stockUnits,
                    StockStatus = st,
                    ShortageKg = shortageKg,
                    ShortageUnits = shortageUnits,
                    Lines = lines,
                });
            }

            res.ProductGroups = groups;
            response.Data = res;
            return response;
        }

        private sealed class LineBucket
        {
            public string LineLabel = "";
            public decimal Kg;
            public decimal Units;
            public decimal? UnitWeightKg;
        }

        private sealed class LineBucketComparer : IEqualityComparer<(int pid, string cutKey, string note)>
        {
            public bool Equals((int pid, string cutKey, string note) x, (int pid, string cutKey, string note) y) =>
                x.pid == y.pid && string.Equals(x.cutKey, y.cutKey, StringComparison.Ordinal)
                && string.Equals(x.note, y.note, StringComparison.Ordinal);

            public int GetHashCode((int pid, string cutKey, string note) obj) =>
                HashCode.Combine(obj.pid, obj.cutKey, obj.note);
        }

        private static bool LineHasQuantity(OrderItem line)
        {
            if (line.PickedQuantity is > 0m) return true;
            return line.Quantity > 0m;
        }

        private static string BuildCutBucketKey(OrderItem line)
        {
            var c = (line.OrderLineCuttingLabel ?? "").Trim();
            var s = (line.OrderLineSizeLabel ?? "").Trim();
            var v = (line.VariantTitle ?? "").Trim();
            var w = line.LineUnitWeightKg?.ToString("G", CultureInfo.InvariantCulture) ?? "";
            return $"{c}\u001e{s}\u001e{v}\u001e{w}";
        }

        private static string BuildLineLabel(OrderItem line)
        {
            var sul = (line.SaleUnitsLine ?? "").Trim();
            if (!string.IsNullOrEmpty(sul))
                return sul;

            var parts = new List<string>();
            var cut = (line.OrderLineCuttingLabel ?? "").Trim();
            if (!string.IsNullOrEmpty(cut)) parts.Add(cut);
            var size = (line.OrderLineSizeLabel ?? "").Trim();
            if (!string.IsNullOrEmpty(size)) parts.Add(size);
            var vt = (line.VariantTitle ?? "").Trim();
            if (!string.IsNullOrEmpty(vt)) parts.Add(vt);
            var per = (line.OrderLinePerUnitWeightLabel ?? "").Trim();
            if (!string.IsNullOrEmpty(per)) parts.Add(per);
            if (parts.Count > 0)
                return string.Join(" · ", parts);

            var title = (line.Title ?? "").Trim();
            return string.IsNullOrEmpty(title) ? "—" : title;
        }

        private static int? PrimaryCategoryId(Product? p)
        {
            if (p?.ProductCategory == null || p.ProductCategory.Count == 0) return null;
            var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
            if (primary != null) return primary.CategoryId;
            return p.ProductCategory.First().CategoryId;
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
                decimal.TryParse(i.SaleTotalWeight.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                return w;

            if (i.LineUnitWeightKg is > 0m && i.Quantity > 0m)
                return i.Quantity * i.LineUnitWeightKg.Value;

            return null;
        }

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

        /// <summary>Simplified stock vs SPA — ok/low/out from quantity + thresholds.</summary>
        private static (decimal? stockKg, decimal? stockUnits, decimal? shortageKg, decimal? shortageUnits, string status)
            ComputeStockAndShortage(Product p, Account? account, decimal totalKg, decimal totalUnits)
        {
            var stock = p.StockQuantity ?? 0m;
            var weighted = p.IsWeighted == true;

            var thr = p.LowStockThreshold is > 0m
                ? p.LowStockThreshold.Value
                : (weighted
                    ? account?.DefaultLowStockThresholdWeighted
                    : account?.DefaultLowStockThresholdUnits) ?? (weighted ? 2m : 3m);

            string Classify(decimal qty)
            {
                if (qty <= 0m) return "out";
                if (thr > 0m && qty <= thr) return "low";
                return "ok";
            }

            decimal? sk = null;
            decimal? su = null;
            decimal? shK = null;
            decimal? shU = null;

            if (weighted && totalKg > 0m)
            {
                sk = Round2(stock);
                var d = totalKg - stock;
                shK = d > 0m ? Round2(d) : null;
            }
            else if (totalUnits > 0m)
            {
                su = Round2(stock);
                var d = totalUnits - stock;
                shU = d > 0m ? Round2(d) : null;
            }
            else if (totalKg > 0m)
            {
                sk = Round2(stock);
                var d = totalKg - stock;
                shK = d > 0m ? Round2(d) : null;
            }

            var st = Classify(stock);
            return (sk, su, shK, shU, st);
        }
    }
}
