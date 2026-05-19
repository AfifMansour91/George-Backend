using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    /// <summary>דוח ריכוז כמויות — הזמנות פתוחות לפי תאריך אספקה, קיבוץ לפי מוצר+אפשרות+הערה.</summary>
    public class QuantityConcentrationReportService : ServiceBase
    {
        private enum PickedFilterMode
        {
            NotPicked,
            Picked,
            All,
        }

        /// <summary>
        /// כותרת שורת פרוט שמייצגת הפרש בין סה&quot;כ המוצר לבין סכום שורות הפירוט (אחרי סינון תצוגה).
        /// </summary>
        public const string RemainderLineLabel = "ללא אפשרות (יתרה)";

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
            string? pickedFilter = null,
            bool includePicked = false,
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
            var pickedMode = ResolvePickedFilter(pickedFilter, includePicked);

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

            var buckets = new Dictionary<(int pid, string cutKey, string note), LineBucket>(new LineBucketComparer());

            foreach (var o in orders)
            {
                foreach (var line in o.OrderItem ?? Enumerable.Empty<OrderItem>())
                {
                    if (line.ProductId is not > 0) continue;
                    if (!LineMatchesPickedFilter(line, pickedMode)) continue;
                    if (!productDict.TryGetValue(line.ProductId.Value, out var p)) continue;
                    var cid = PrimaryCategoryId(p);
                    if (catFilter != null && cid != catFilter) continue;
                    if (!LineHasQuantity(line)) continue;

                    var (kg, units) = SplitLineQty(line, p);
                    if (kg <= 0m && units <= 0m) continue;

                    var note = (line.Notes ?? "").Trim();
                    var cutKey = BuildCutBucketKey(line, p.Name);
                    var key = (line.ProductId.Value, cutKey, note);
                    if (!buckets.TryGetValue(key, out var b))
                    {
                        b = new LineBucket
                        {
                            LineLabel = OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, p.Name) ?? "",
                        };
                        buckets[key] = b;
                    }

                    b.Kg += kg;
                    b.Units += units;
                    if (IsWeightedSoldByUnits(line))
                    {
                        if (b.UnitWeightKg == null && line.LineUnitWeightKg is > 0m)
                            b.UnitWeightKg = line.LineUnitWeightKg;
                        else if (b.UnitWeightKg == null && line.UnitWeightGrams is > 0m)
                            b.UnitWeightKg = line.UnitWeightGrams.Value / 1000m;
                    }
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

                lines = CollapseIfSingleSyntheticLine(lines, p.Name ?? "");
                lines = FilterAndMergeDetailLines(lines, p);
                lines = AppendRemainderDetailLineIfNeeded(lines, sumKg, sumUnits);

                var (stockKg, stockUnits, shortageKg, shortageUnits, st) =
                    ComputeStockAndShortage(p, account, sumKg, sumUnits);

                var totalUnitsOut = sumUnits > 0m ? Round2(sumUnits) : (decimal?)null;
                if (p.IsWeighted == true && sumKg > 0m && lines.Count == 0)
                    totalUnitsOut = null;

                groups.Add(new QuantityConcentrationProductGroupDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name ?? "",
                    CategoryId = PrimaryCategoryId(p) ?? 0,
                    TotalQuantityKg = sumKg > 0m ? Round2(sumKg) : null,
                    TotalQuantityUnits = totalUnitsOut,
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
            if (line.Quantity > 0m) return true;
            if (line.LineUnit is > 0m) return true;
            if (!string.IsNullOrWhiteSpace(line.SaleTotalWeight) &&
                decimal.TryParse(line.SaleTotalWeight.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var w) &&
                w > 0m)
                return true;
            return line.PickedQuantity is > 0m;
        }

        /// <summary>
        /// Bucket key — real catalog option only; otherwise one bucket per product (units/kg on parent row).
        /// </summary>
        private static string BuildCutBucketKey(OrderItem line, string? productName)
        {
            var label = OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, productName);
            if (!string.IsNullOrEmpty(label))
                return AttrDedupeKey(label);
            return "\u001eno_structured_option";
        }

        private static List<QuantityConcentrationLineDto> FilterAndMergeDetailLines(
            List<QuantityConcentrationLineDto> lines,
            Product p)
        {
            var pn = (p.Name ?? "").Trim();
            var filtered = lines.Where(l =>
            {
                if (!string.IsNullOrEmpty(l.Note)) return true;
                if (IsSyntheticLineLabel(l.LineLabel, pn)) return false;
                if (p.IsWeighted == true && OrderItemReportLineLabel.IsNonOptionDisplayLabel(l.LineLabel))
                    return false;
                return true;
            }).ToList();

            return MergeLinesByCanonicalLabel(filtered);
        }

        private static List<QuantityConcentrationLineDto> MergeLinesByCanonicalLabel(List<QuantityConcentrationLineDto> lines) =>
            lines
                .GroupBy(l => (l.Note ?? "", AttrDedupeKey(l.LineLabel.Trim())))
                .Select(g =>
                {
                    var list = g.ToList();
                    if (list.Count == 1)
                    {
                        var only = list[0];
                        return new QuantityConcentrationLineDto
                        {
                            LineLabel = only.LineLabel,
                            Note = only.Note,
                            WeightPerUnitKg = only.WeightPerUnitKg,
                            QuantityKg = only.QuantityKg,
                            QuantityUnits = only.QuantityUnits,
                        };
                    }

                    var kg = list.Sum(x => x.QuantityKg ?? 0m);
                    var u = list.Sum(x => x.QuantityUnits ?? 0m);
                    var wpu = list.Select(x => x.WeightPerUnitKg).FirstOrDefault(x => x is > 0m);
                    var bestLabel = list.OrderByDescending(x => x.LineLabel.Length).First().LineLabel;
                    return new QuantityConcentrationLineDto
                    {
                        LineLabel = bestLabel,
                        Note = list[0].Note,
                        WeightPerUnitKg = wpu is > 0m ? wpu : null,
                        QuantityKg = kg > 0m ? Round2(kg) : null,
                        QuantityUnits = u > 0m ? Round2(u) : null,
                    };
                })
                .OrderBy(x => x.LineLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>For tests: same pipeline as <see cref="GetReportAsync"/> detail rows after collapsing single synthetic line.</summary>
        public static List<QuantityConcentrationLineDto> ApplyDetailLineDisplayRules(
            List<QuantityConcentrationLineDto> lines,
            Product p)
        {
            lines = CollapseIfSingleSyntheticLine(lines, p.Name ?? "");
            return FilterAndMergeDetailLines(lines, p);
        }

        /// <summary>
        /// כאשר סכום שורות הפירוט אחרי <see cref="FilterAndMergeDetailLines"/> נמוך מסה&quot;כ המוצר (סיכום מהזמנות) —
        /// מוסיף שורה אחת עם הפרש הק&quot;ג והיחידות.
        /// רק אם כבר יש לפחות שורת פירוט אחת: בלי פירוט — שורת האב מספיקה ואין לשכפל את כל הסה&quot;כ בשורת יתרה.
        /// </summary>
        public static List<QuantityConcentrationLineDto> AppendRemainderDetailLineIfNeeded(
            List<QuantityConcentrationLineDto> lines,
            decimal sumKgRaw,
            decimal sumUnitsRaw)
        {
            if (lines.Count == 0)
                return lines;

            var detKg = lines.Sum(l => l.QuantityKg ?? 0m);
            var detU = lines.Sum(l => l.QuantityUnits ?? 0m);
            var totalKg = sumKgRaw > 0m ? Round2(sumKgRaw) : 0m;
            var totalU = sumUnitsRaw > 0m ? Round2(sumUnitsRaw) : 0m;
            var remKg = totalKg - detKg;
            var remU = totalU - detU;
            const decimal tol = 0.02m;
            if (remKg < -tol || remU < -tol)
                return lines;

            decimal? kgOut = remKg > tol ? Round2(remKg) : null;
            decimal? uOut = remU > tol ? Round2(remU) : null;
            if (kgOut == null && uOut == null)
                return lines;

            var copy = lines.ToList();
            copy.Add(new QuantityConcentrationLineDto
            {
                LineLabel = RemainderLineLabel,
                WeightPerUnitKg = null,
                QuantityKg = kgOut,
                QuantityUnits = uOut,
                Note = null,
            });
            return copy;
        }

        /// <summary>Aligned with shop-manager orderItemLineDisplay: weight-total lines are not "sold by units".</summary>
        private static bool IsWeightedSoldByUnits(OrderItem line)
        {
            var mode = (line.OrderLineQuantityMode ?? "").Trim().ToLowerInvariant();
            if (mode == "weight") return false;

            var grams = GramsPerUnitFromOrderItem(line);
            if (grams <= 0m) return false;

            if (mode == "units") return true;
            if (!string.IsNullOrEmpty(mode)) return false;

            if (SaleUnitsIndicatesPieceSale(line.SaleUnits)) return true;
            if (!string.IsNullOrWhiteSpace(line.OrderLinePerUnitWeightLabel)) return true;

            if (line.UnitWeightGrams is not > 0m) return false;

            var q = line.Quantity;
            var qWhole = Math.Abs(q - Math.Round(q)) < 0.0001m;
            var qInt = qWhole ? (int)Math.Round(q) : (int?)null;

            if (qInt is >= 2) return true;
            if (qInt == 1)
            {
                if (SaleTotalWeightIndicatesOrderTotalKg(line.SaleTotalWeight) && !SaleUnitsIndicatesPieceSale(line.SaleUnits))
                    return false;
                return true;
            }

            return false;
        }

        private static decimal GramsPerUnitFromOrderItem(OrderItem line)
        {
            if (line.UnitWeightGrams is > 0m) return line.UnitWeightGrams.Value;
            if (line.LineUnitWeightKg is > 0m) return line.LineUnitWeightKg.Value * 1000m;
            var fromPer = ParseGramsFromHebrewWeightLabel(line.OrderLinePerUnitWeightLabel);
            if (fromPer > 0m) return fromPer;
            var fromSize = ParseGramsFromHebrewWeightLabel(line.OrderLineSizeLabel);
            if (fromSize > 0m) return fromSize;

            if (string.Equals(line.OrderLineQuantityMode, "units", StringComparison.OrdinalIgnoreCase))
            {
                var st = line.SaleTotalWeight?.Trim();
                if (!string.IsNullOrEmpty(st))
                {
                    var g = ParseGramsFromHebrewWeightLabel(st);
                    if (g > 0m) return g;
                    if (decimal.TryParse(st, NumberStyles.Any, CultureInfo.InvariantCulture, out var bare) && bare > 0m)
                    {
                        if (st.Contains('.') || bare < 50m)
                            return bare * 1000m;
                        if (bare >= 50m && bare <= 100000m)
                            return bare;
                    }
                }
            }

            return 0m;
        }

        private static decimal ParseGramsFromHebrewWeightLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return 0m;
            var s = label.Trim();
            var kgM = Regex.Match(s, @"([\d.,]+)\s*ק[״""\u0022']?\s*ג");
            if (kgM.Success && decimal.TryParse(kgM.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var kg))
            {
                if (kg > 0m) return Math.Round(kg * 1000m, 4);
            }
            var gM = Regex.Match(s, @"([\d.,]+)\s*ג[ר]?ם");
            if (gM.Success && decimal.TryParse(gM.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var g))
            {
                if (g > 0m) return Math.Round(g, 4);
            }
            var gApo = Regex.Match(s, @"([\d.,]+)\s*גר['׳`]");
            if (gApo.Success && decimal.TryParse(gApo.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ga))
            {
                if (ga > 0m) return Math.Round(ga, 4);
            }
            return 0m;
        }

        private static bool SaleUnitsIndicatesPieceSale(string? saleUnits)
        {
            var s = saleUnits?.Trim();
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Contains("יח", StringComparison.Ordinal)) return true;
            if (Regex.IsMatch(s, @"\b(pcs?|pieces?|ea\.?)\b", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(s, @"\d\s*[×x]|[×x]\s*\d", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        private static bool SaleTotalWeightIndicatesOrderTotalKg(string? saleTotalWeight)
        {
            var s = saleTotalWeight?.Trim();
            if (string.IsNullOrEmpty(s)) return false;
            if (Regex.IsMatch(s, @"ק\s*[""״']?\s*ג")) return true;
            if (Regex.IsMatch(s, @"\bקג\b", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        private static string AttrDedupeKey(string s)
        {
            var t = s.Trim().Normalize(NormalizationForm.FormKC);
            t = Regex.Replace(t, @"[\u201C\u201D\u201E\u0022״]", "\"");
            t = t.Replace('׳', '\'');
            t = Regex.Replace(t, @"\s+", " ");
            return t.ToLowerInvariant();
        }

        public static string BuildLineLabel(OrderItem line, string? productName = null) =>
            OrderItemReportLineLabel.BuildLineLabel(line, productName);

        public static List<QuantityConcentrationLineDto> CollapseIfSingleSyntheticLine(
            List<QuantityConcentrationLineDto> lines,
            string productName)
        {
            if (lines.Count != 1) return lines;
            var L = lines[0];
            if (!string.IsNullOrEmpty(L.Note)) return lines;
            if (IsSyntheticLineLabel(L.LineLabel, productName)) return new List<QuantityConcentrationLineDto>();
            if (OrderItemReportLineLabel.IsNonOptionDisplayLabel(L.LineLabel))
                return new List<QuantityConcentrationLineDto>();
            return lines;
        }

        private static bool IsSyntheticLineLabel(string label, string productName)
        {
            var t = label.Trim();
            if (t is "—" or "-" or "–") return true;
            var pn = productName.Trim();
            if (pn.Length > 0 && string.Equals(t, pn, StringComparison.OrdinalIgnoreCase))
                return true;
            if (pn.Length > 0 &&
                t.StartsWith("--", StringComparison.Ordinal) &&
                t.EndsWith("--", StringComparison.Ordinal))
            {
                var inner = t.TrimStart('-', '–', '—', ' ').TrimEnd('-', '–', '—', ' ').Trim();
                if (string.Equals(inner, pn, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int? PrimaryCategoryId(Product? p)
        {
            if (p?.ProductCategory == null || p.ProductCategory.Count == 0) return null;
            var primary = p.ProductCategory.FirstOrDefault(x => x.IsPrimary);
            if (primary != null) return primary.CategoryId;
            return p.ProductCategory.First().CategoryId;
        }

        public static (decimal kg, decimal units) SplitLineQty(OrderItem line, Product p)
        {
            var mode = (line.OrderLineQuantityMode ?? "").Trim().ToLowerInvariant();
            if (mode == "weight")
            {
                var kg = LineWeightKg(line);
                return (kg ?? 0m, 0m);
            }

            if (mode == "units")
            {
                var u = line.LineUnit ?? line.Quantity;
                if (IsWeightedSoldByUnits(line))
                {
                    var kg = LineWeightKg(line) ?? TotalKgFromGramsPerUnit(line);
                    return (kg ?? 0m, u);
                }
                if (p.IsWeighted == true)
                {
                    var kgW = LineWeightKg(line) ?? TotalKgFromGramsPerUnit(line);
                    if (kgW is > 0m)
                        return (kgW.Value, 0m);
                }
                return (0m, u);
            }

            if (p.IsWeighted == true)
            {
                var kg = LineWeightKg(line) ?? TotalKgFromGramsPerUnit(line);
                if (kg is > 0m)
                {
                    if (IsWeightedSoldByUnits(line))
                        return (kg.Value, line.LineUnit ?? line.Quantity);
                    return (kg.Value, 0m);
                }
            }

            var kgLoose = LineWeightKg(line) ?? TotalKgFromGramsPerUnit(line);
            var unitsLoose = line.LineUnit ?? line.Quantity;
            return (kgLoose ?? 0m, unitsLoose);
        }

        private static decimal? LineWeightKg(OrderItem i)
        {
            if (i.PickedQuantity is > 0m &&
                string.Equals(i.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
                return i.PickedQuantity.Value;

            if (i.UnitWeightGrams is > 0m && i.Quantity > 0m)
                return i.Quantity * (i.UnitWeightGrams.Value / 1000m);

            if (!string.IsNullOrWhiteSpace(i.SaleTotalWeight) &&
                decimal.TryParse(i.SaleTotalWeight.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var w) &&
                w > 0m)
                return w;

            if (i.LineUnitWeightKg is > 0m && i.Quantity > 0m)
                return i.Quantity * i.LineUnitWeightKg.Value;

            return null;
        }

        private static decimal? TotalKgFromGramsPerUnit(OrderItem line)
        {
            var g = GramsPerUnitFromOrderItem(line);
            if (g <= 0m || line.Quantity <= 0m) return null;
            return line.Quantity * (g / 1000m);
        }

        private static decimal Round2(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);

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

            var useKgForStock = weighted && totalKg > 0m;
            if (useKgForStock)
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

        private static PickedFilterMode ResolvePickedFilter(string? pickedFilter, bool includePicked)
        {
            if (!string.IsNullOrWhiteSpace(pickedFilter))
            {
                return pickedFilter.Trim().ToLowerInvariant() switch
                {
                    "all" => PickedFilterMode.All,
                    "picked" => PickedFilterMode.Picked,
                    "notpicked" or "not_picked" => PickedFilterMode.NotPicked,
                    _ => PickedFilterMode.NotPicked,
                };
            }

            return includePicked ? PickedFilterMode.All : PickedFilterMode.NotPicked;
        }

        private static bool LineMatchesPickedFilter(OrderItem line, PickedFilterMode mode) =>
            mode switch
            {
                PickedFilterMode.All => true,
                PickedFilterMode.Picked => line.PickingUserConfirmed,
                _ => !line.PickingUserConfirmed,
            };
    }
}
