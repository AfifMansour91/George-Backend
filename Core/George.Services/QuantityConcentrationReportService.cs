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
                    var variant = FindVariant(p, line);
                    var optionLabel = OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, p.Name);
                    // Weight-named catalog options ("500 גרם", "1 ק\"ג") read as quantity text and resolve
                    // to null — when the line still maps to a catalog variant, its catalog label is the
                    // real option name and must get its own detail bucket.
                    if (string.IsNullOrEmpty(optionLabel) && variant != null)
                    {
                        var catalogLabel = FormatVariantLabel(variant);
                        if (!catalogLabel.StartsWith("#", StringComparison.Ordinal))
                            optionLabel = catalogLabel;
                    }

                    var cutKey = !string.IsNullOrEmpty(optionLabel) ? AttrDedupeKey(optionLabel) : NoStructuredOptionKey;
                    var key = (line.ProductId.Value, cutKey, note);
                    if (!buckets.TryGetValue(key, out var b))
                    {
                        b = new LineBucket { LineLabel = optionLabel ?? "" };
                        buckets[key] = b;
                    }

                    if (variant != null)
                        b.VariantIds.Add(variant.Id);

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

                var stockDisplayMode = ResolveStockDisplayMode(p);
                var variantQtyStock = UsesVariationQuantityStock(p);

                decimal sumKg = 0m, sumUnits = 0m;
                var lines = new List<QuantityConcentrationLineDto>();
                foreach (var kv in g.OrderBy(x => x.Value.LineLabel, StringComparer.OrdinalIgnoreCase))
                {
                    var b = kv.Value;
                    if (b.Kg > 0m) sumKg += b.Kg;
                    if (b.Units > 0m) sumUnits += b.Units;
                    var variantId = b.VariantIds.Count > 0 ? b.VariantIds.Min() : (int?)null;
                    var (lineSk, lineSu) = ResolveLineCatalogStock(p, stockDisplayMode, variantId);
                    var lineLabel = b.LineLabel;
                    var noteText = string.IsNullOrEmpty(kv.Key.note) ? null : kv.Key.note;
                    lines.Add(new QuantityConcentrationLineDto
                    {
                        LineLabel = lineLabel,
                        WeightPerUnitKg = b.UnitWeightKg is > 0m ? Round2(b.UnitWeightKg.Value) : null,
                        QuantityKg = b.Kg > 0m ? Round2(b.Kg) : null,
                        QuantityUnits = b.Units > 0m ? Round2(b.Units) : null,
                        Note = noteText,
                        ShowUnitsInTotalQuantity = ShowUnitsInTotalQuantityForTotalQtyColumn(p, b.Units),
                        StockKg = lineSk,
                        StockUnits = lineSu,
                        VariantId = variantId,
                    });
                }

                lines = CollapseIfSingleSyntheticLine(lines, p.Name ?? "");
                lines = FilterAndMergeDetailLines(lines, p);
                lines = EnrichVariationQuantityStockLines(p, lines, stockDisplayMode);
                lines = AppendRemainderDetailLineIfNeeded(lines, sumKg, sumUnits, p);
                AssignLineDisplayKinds(p, lines);

                var orderVariantIds = g.SelectMany(kv => kv.Value.VariantIds).ToHashSet();
                var (stockKg, stockUnits, shortageKg, shortageUnits, st) =
                    ComputeStockAndShortage(p, account, sumKg, sumUnits, orderVariantIds, stockDisplayMode);

                var showUnitsInTotal = ShowUnitsInTotalQuantityForTotalQtyColumn(p, sumUnits);
                var totalUnitsOut = sumUnits > 0m ? Round2(sumUnits) : (decimal?)null;
                var lineStockUnitLabel = StockUnitLabelForProduct(p, variantQtyStock);
                var parentUnitWeightKg = ResolveNoVariationParentUnitWeightKg(
                    p, g.Select(kv => kv.Value.UnitWeightKg));

                groups.Add(new QuantityConcentrationProductGroupDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name ?? "",
                    CategoryId = PrimaryCategoryId(p) ?? 0,
                    TotalQuantityKg = sumKg > 0m ? Round2(sumKg) : null,
                    TotalQuantityUnits = totalUnitsOut,
                    ShowUnitsInTotalQuantity = showUnitsInTotal,
                    ShowWeightPerUnitColumn = parentUnitWeightKg is > 0m || lines.Any(l =>
                        string.Equals(l.LineDisplayKind, "variant", StringComparison.Ordinal)
                        && l.WeightPerUnitKg is > 0m),
                    WeightPerUnitKg = parentUnitWeightKg is > 0m ? Round2(parentUnitWeightKg.Value) : null,
                    StockKg = stockKg,
                    StockUnits = stockUnits,
                    StockDisplayMode = stockDisplayMode,
                    StockUnitLabel = lineStockUnitLabel,
                    StockManagementType = ProductCatalogStockClassification.StockManagementTypeForApi(p),
                    VariationStockByQuantity = p.VariationStockByQuantity == true,
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
            public readonly HashSet<int> VariantIds = new();
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
        private const string NoStructuredOptionKey = "\u001eno_structured_option";

        /// <summary>
        /// מוצר עם וריאציות בקטלוג (חיתוכים/אפשרויות) — בלי קשר לסוג ניהול המלאי (כמות/סטטוס/וריאציה).
        /// </summary>
        private static bool ProductHasCatalogVariationOptions(Product p) =>
            ProductCatalogStockClassification.ActiveVariants(p).Count > 0;

        /// <summary>
        /// משקל ליחידה (ק&quot;ג) לשורת האב של מוצר שקיל בלי וריאציות (למשל צלעות טלה):
        /// מהשורות בהזמנות כשכולן באותו משקל, אחרת מהגדרת המשקל בקטלוג. מוצר עם וריאציות — null
        /// (המשקל מוצג פר וריאציה בשורות הפירוט).
        /// </summary>
        public static decimal? ResolveNoVariationParentUnitWeightKg(
            Product p,
            IEnumerable<decimal?> bucketUnitWeightsKg)
        {
            if (ProductHasCatalogVariationOptions(p)) return null;

            var weights = bucketUnitWeightsKg
                .Where(w => w is > 0m)
                .Select(w => Math.Round(w!.Value, 3))
                .Distinct()
                .ToList();
            if (weights.Count == 1) return weights[0];
            if (weights.Count > 1) return null;
            return CatalogUnitWeightKg(p);
        }

        /// <summary>Fixed/average per-unit weight (kg) from catalog config for weighted by-unit products.</summary>
        private static decimal? CatalogUnitWeightKg(Product p)
        {
            if (p.IsWeighted != true) return null;
            var setup = (p.SetupType?.Name ?? "").Trim().ToLowerInvariant();
            if (setup != "by_unit" && setup != "by_unit_and_weight") return null;
            var wc = p.WeightConfig;
            if (wc == null || wc.WeightByVariant == true) return null;
            if (!decimal.TryParse((wc.UnitWeight ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var w) || w <= 0m)
                return null;
            var unit = (wc.Unit?.Name ?? "kg").Trim().ToLowerInvariant();
            return unit == "g" ? w / 1000m : w;
        }

        private static List<QuantityConcentrationLineDto> FilterAndMergeDetailLines(
            List<QuantityConcentrationLineDto> lines,
            Product p)
        {
            var pn = (p.Name ?? "").Trim();

            if (!ProductHasCatalogVariationOptions(p))
            {
                var kept = lines.Where(l =>
                    !string.IsNullOrEmpty(l.Note)
                    || (l.QuantityKg ?? 0) > 0
                    || (l.QuantityUnits ?? 0) > 0).ToList();

                var useNoteBuckets = kept.Count > 1 && (
                    kept.Any(l => !string.IsNullOrEmpty(l.Note))
                    || kept
                        .Select(l => l.WeightPerUnitKg)
                        .Where(w => w is > 0m)
                        .Distinct()
                        .Count() > 1
                        && kept.All(l => string.IsNullOrEmpty(l.Note)));

                if (useNoteBuckets)
                {
                    foreach (var l in kept)
                    {
                        l.LineDisplayKind = "noteBucket";
                        l.LineLabel = "";
                    }
                    return MergeLinesByNoteAndWeight(kept);
                }

                if (!string.IsNullOrEmpty(pn))
                {
                    foreach (var l in kept)
                        l.LineLabel = pn;
                }

                return MergeLinesByCanonicalLabel(kept);
            }

            var filtered = lines.Where(l =>
            {
                if (!string.IsNullOrEmpty(l.Note)) return true;
                if (IsSyntheticLineLabel(l.LineLabel, pn)) return false;
                // Variant-linked lines keep weight-looking labels — catalog options may be NAMED as
                // weights ("500 גרם") and must still show as real option rows.
                if (p.IsWeighted == true
                    && l.VariantId is not > 0
                    && OrderItemReportLineLabel.IsNonOptionDisplayLabel(l.LineLabel))
                    return false;
                return true;
            }).ToList();

            return MergeLinesByCanonicalLabel(filtered);
        }

        private static List<QuantityConcentrationLineDto> MergeLinesByNoteAndWeight(List<QuantityConcentrationLineDto> lines) =>
            lines
                .GroupBy(l => (l.Note ?? "", l.WeightPerUnitKg))
                .Select(g =>
                {
                    var list = g.ToList();
                    if (list.Count == 1)
                        return list[0];

                    var kg = list.Sum(x => x.QuantityKg ?? 0m);
                    var u = list.Sum(x => x.QuantityUnits ?? 0m);
                    var wpu = list[0].WeightPerUnitKg;
                    return new QuantityConcentrationLineDto
                    {
                        LineLabel = "",
                        LineDisplayKind = "noteBucket",
                        Note = list[0].Note,
                        WeightPerUnitKg = wpu is > 0m ? wpu : null,
                        QuantityKg = kg > 0m ? Round2(kg) : null,
                        QuantityUnits = u > 0m ? Round2(u) : null,
                        VariantId = list.Select(x => x.VariantId).FirstOrDefault(x => x is > 0),
                        StockKg = MergeStockField(list, l => l.StockKg),
                        StockUnits = MergeStockField(list, l => l.StockUnits),
                        StockUnitLabel = list.Select(x => x.StockUnitLabel).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    };
                })
                .OrderBy(x => x.WeightPerUnitKg ?? 0m)
                .ThenBy(x => x.Note ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static List<QuantityConcentrationLineDto> MergeLinesByCanonicalLabel(List<QuantityConcentrationLineDto> lines) =>
            lines
                .GroupBy(l => (l.Note ?? "", AttrDedupeKey(l.LineLabel.Trim())))
                .Select(g =>
                {
                    var list = g.ToList();
                    if (list.Count == 1)
                        return list[0];

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
                        VariantId = list.Select(x => x.VariantId).FirstOrDefault(x => x is > 0),
                        StockKg = MergeStockField(list, l => l.StockKg),
                        StockUnits = MergeStockField(list, l => l.StockUnits),
                    };
                })
                .OrderBy(x => x.LineLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>For tests: variation stock enrichment step only.</summary>
        public static List<QuantityConcentrationLineDto> EnrichDetailLinesWithVariationStock(
            List<QuantityConcentrationLineDto> lines,
            Product p)
        {
            var mode = ProductCatalogStockClassification.UsesNumericStockDisplay(p) ? "quantity" : "status";
            return EnrichVariationQuantityStockLines(p, lines, mode);
        }

        /// <summary>For tests: same pipeline as <see cref="GetReportAsync"/> detail rows after collapsing single synthetic line.</summary>
        public static List<QuantityConcentrationLineDto> ApplyDetailLineDisplayRules(
            List<QuantityConcentrationLineDto> lines,
            Product p)
        {
            lines = CollapseIfSingleSyntheticLine(lines, p.Name ?? "");
            lines = FilterAndMergeDetailLines(lines, p);
            AssignLineDisplayKinds(p, lines);
            return lines;
        }

        /// <summary>
        /// כאשר סכום שורות הפירוט אחרי <see cref="FilterAndMergeDetailLines"/> נמוך מסה&quot;כ המוצר (סיכום מהזמנות) —
        /// מוסיף שורה אחת עם הפרש הק&quot;ג והיחידות.
        /// רק אם כבר יש לפחות שורת פירוט אחת: בלי פירוט — שורת האב מספיקה ואין לשכפל את כל הסה&quot;כ בשורת יתרה.
        /// </summary>
        public static List<QuantityConcentrationLineDto> AppendRemainderDetailLineIfNeeded(
            List<QuantityConcentrationLineDto> lines,
            decimal sumKgRaw,
            decimal sumUnitsRaw,
            Product? product = null)
        {
            if (lines.Count == 0)
                return lines;

            if (product != null && !ProductHasCatalogVariationOptions(product))
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
                LineDisplayKind = "remainder",
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

        private static string AttrDedupeKey(string s) => ProductCatalogVariantResolution.AttrDedupeKey(s);

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
            if (OrderItemReportLineLabel.IsNonOptionDisplayLabel(L.LineLabel) && L.VariantId is not > 0)
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
                if (ProductCatalogStockClassification.IsWeightedLikeProduct(p))
                {
                    var kgW = LineWeightKg(line) ?? TotalKgFromGramsPerUnit(line);
                    if (kgW is > 0m)
                        return (kgW.Value, 0m);
                }
                return (0m, u);
            }

            if (ProductCatalogStockClassification.IsWeightedLikeProduct(p))
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

        private static ProductVariant? FindVariant(Product p, OrderItem line) =>
            ProductCatalogVariantResolution.FindVariantForOrderLine(p, line);

        private static string FormatVariantLabel(ProductVariant v) =>
            ProductCatalogVariantResolution.FormatVariantDisplayLabel(v);

        /// <summary>Unit count in total-qty column only for non-weighted products; parent units column is separate.</summary>
        private static bool ShowUnitsInTotalQuantityForTotalQtyColumn(Product p, decimal sumUnits)
        {
            if (sumUnits <= 0m)
                return false;
            if (p.IsWeighted == true)
                return false;
            return true;
        }

        private static void AssignLineDisplayKinds(Product p, List<QuantityConcentrationLineDto> lines)
        {
            var hasVariants = ProductHasCatalogVariationOptions(p);
            var stockUnitLabel = StockUnitLabelForProduct(p, UsesVariationQuantityStock(p));

            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line.StockUnitLabel) && (line.StockKg != null || line.StockUnits != null))
                {
                    /* keep line stock unit label */
                }
                else if (line.StockKg != null || line.StockUnits != null)
                {
                    line.StockUnitLabel = stockUnitLabel;
                }

                if (!string.IsNullOrEmpty(line.LineDisplayKind))
                    continue;

                if (string.Equals(line.LineLabel, RemainderLineLabel, StringComparison.Ordinal))
                {
                    line.LineDisplayKind = "remainder";
                    continue;
                }

                if (string.Equals(line.LineDisplayKind, "noteBucket", StringComparison.Ordinal)
                    || (string.IsNullOrWhiteSpace(line.LineLabel) && line.WeightPerUnitKg is > 0m))
                {
                    line.LineDisplayKind = "noteBucket";
                    line.LineLabel = "";
                    continue;
                }

                if (hasVariants && line.VariantId is > 0)
                {
                    line.LineDisplayKind = "variant";
                    line.LineLabel = StripEmbeddedWeightFromVariantLabel(line.LineLabel);
                    continue;
                }

                if (hasVariants)
                {
                    line.LineDisplayKind = "variant";
                    line.LineLabel = StripEmbeddedWeightFromVariantLabel(line.LineLabel);
                    continue;
                }

                if (line.WeightPerUnitKg is > 0m && string.IsNullOrWhiteSpace(line.LineLabel))
                {
                    line.LineDisplayKind = "noteBucket";
                    continue;
                }

                line.LineDisplayKind = "weightChoice";
            }
        }

        private static string StripEmbeddedWeightFromVariantLabel(string label)
        {
            var t = label.Trim();
            if (t.Length == 0) return t;
            return Regex.Replace(t, @"\s*\(כ[\s-]*[\d.,]+\s*גר['׳`]?\)\s*$", "", RegexOptions.IgnoreCase).Trim();
        }

        private static decimal? MergeStockField(
            List<QuantityConcentrationLineDto> list,
            Func<QuantityConcentrationLineDto, decimal?> selector)
        {
            if (!list.Any(x => selector(x).HasValue))
                return null;
            return Round2(list.Sum(x => selector(x) ?? 0m));
        }

        /// <summary>
        /// Variation + quantity: ensure every catalog variant appears with live stock (even if not in orders).
        /// </summary>
        private static List<QuantityConcentrationLineDto> EnrichVariationQuantityStockLines(
            Product p,
            List<QuantityConcentrationLineDto> lines,
            string stockDisplayMode)
        {
            if (stockDisplayMode != "quantity" || !UsesVariationQuantityStock(p))
                return lines;

            var variants = ProductCatalogStockClassification.ActiveVariants(p);
            var result = new List<QuantityConcentrationLineDto>(lines);
            var stockUnitLabel = StockUnitLabelForProduct(p, true);
            var stockByKg = StockDisplayUsesKg(p);

            foreach (var v in variants.OrderBy(FormatVariantLabel, StringComparer.OrdinalIgnoreCase))
            {
                var label = FormatVariantLabel(v);
                var stockQty = Round2(v.StockQuantity ?? 0m);
                var labelKey = AttrDedupeKey(label);

                var existing = result.FirstOrDefault(l =>
                    l.VariantId == v.Id
                    || AttrDedupeKey(l.LineLabel.Trim()) == labelKey);

                if (existing != null)
                {
                    existing.VariantId = v.Id;
                    if (stockByKg)
                    {
                        existing.StockKg = stockQty;
                        existing.StockUnits = null;
                    }
                    else
                    {
                        existing.StockUnits = stockQty;
                        existing.StockKg = null;
                    }
                    existing.StockUnitLabel = stockUnitLabel;
                    if (string.IsNullOrWhiteSpace(existing.LineLabel) || IsSyntheticLineLabel(existing.LineLabel, p.Name ?? ""))
                        existing.LineLabel = label;
                    continue;
                }

                result.Add(new QuantityConcentrationLineDto
                {
                    LineLabel = label,
                    VariantId = v.Id,
                    StockKg = stockByKg ? stockQty : null,
                    StockUnits = stockByKg ? null : stockQty,
                    StockUnitLabel = stockUnitLabel,
                    ShowUnitsInTotalQuantity = ShowUnitsInTotalQuantityForTotalQtyColumn(p, 0m),
                });
            }

            return result
                .OrderByDescending(l => (l.QuantityKg ?? 0m) > 0m || (l.QuantityUnits ?? 0m) > 0m)
                .ThenBy(l => l.LineLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolveStockDisplayMode(Product p) =>
            ProductCatalogStockClassification.UsesNumericStockDisplay(p) ? "quantity" : "status";

        private static bool UsesVariationQuantityStock(Product p) =>
            ProductCatalogStockClassification.IsVariationManagement(p)
            && ProductCatalogStockClassification.ActiveVariants(p).Count > 0
            && p.VariationStockByQuantity == true;

        private static bool StockDisplayUsesKg(Product p) =>
            ProductCatalogStockClassification.IsWeightedLikeProduct(p);

        private static string? StockUnitLabelForProduct(Product p, bool variantQuantityStock)
        {
            if (variantQuantityStock)
            {
                if (StockDisplayUsesKg(p))
                    return string.IsNullOrWhiteSpace(p.WeightUnit) ? "ק״ג" : p.WeightUnit!.Trim();
                return "יח׳";
            }
            if (StockDisplayUsesKg(p))
                return string.IsNullOrWhiteSpace(p.WeightUnit) ? "ק״ג" : p.WeightUnit!.Trim();
            var setup = (p.SetupType?.Name ?? "").Trim().ToLowerInvariant();
            if (setup == "by_weight")
                return string.IsNullOrWhiteSpace(p.WeightUnit) ? "ק״ג" : p.WeightUnit!.Trim();
            return "יח׳";
        }

        private static (decimal? kg, decimal? units) ResolveCatalogStockDisplay(Product p, HashSet<int> orderVariantIds)
        {
            var variants = ProductCatalogStockClassification.ActiveVariants(p);
            if (UsesVariationQuantityStock(p))
            {
                var sum = variants.Sum(v => v.StockQuantity ?? 0m);
                if (StockDisplayUsesKg(p))
                    return (Round2(sum), null);
                return (null, Round2(sum));
            }

            var qty = p.StockQuantity ?? 0m;
            if (StockDisplayUsesKg(p))
                return (Round2(qty), null);
            return (null, Round2(qty));
        }

        private static (decimal? kg, decimal? units) ResolveLineCatalogStock(
            Product p,
            string stockDisplayMode,
            int? variantId)
        {
            if (stockDisplayMode != "quantity" || variantId is not > 0)
                return (null, null);

            if (!UsesVariationQuantityStock(p))
                return (null, null);

            var v = p.ProductVariant?.FirstOrDefault(x => !x.IsDeleted && x.Id == variantId);
            if (v == null) return (null, null);
            var qty = Round2(v.StockQuantity ?? 0m);
            if (StockDisplayUsesKg(p))
                return (qty, null);
            return (null, qty);
        }

        private static decimal ResolveAvailableUnits(Product p, HashSet<int> orderVariantIds)
        {
            var variants = ProductCatalogStockClassification.ActiveVariants(p);
            if (ProductCatalogStockClassification.IsVariationManagement(p) && variants.Count > 0)
            {
                if (p.VariationStockByQuantity != true)
                    return 0m;

                var relevant = orderVariantIds.Count > 0
                    ? variants.Where(v => orderVariantIds.Contains(v.Id))
                    : variants;
                return relevant.Sum(v => v.StockQuantity ?? 0m);
            }

            return p.StockQuantity ?? 0m;
        }

        private static decimal ResolveAvailableKg(Product p, HashSet<int> orderVariantIds)
        {
            _ = orderVariantIds;
            return p.StockQuantity ?? 0m;
        }

        private static bool ShouldCompareDemandInUnits(Product p, decimal totalKg, decimal totalUnits)
        {
            if (totalUnits <= 0m)
                return false;

            if (ProductCatalogStockClassification.IsVariationManagement(p)
                && ProductCatalogStockClassification.ActiveVariants(p).Count > 0
                && p.VariationStockByQuantity == true)
                return true;

            if (p.IsWeighted != true)
                return true;

            // שקיל לפי יחידה — גם כשמוצג גם ק"ג בדוח
            return totalKg > 0m;
        }

        /// <summary>
        /// Live catalog stock for the report row — never infer shortage when stock is status-only or unknown quantity.
        /// </summary>
        private static (decimal? stockKg, decimal? stockUnits, decimal? shortageKg, decimal? shortageUnits, string status)
            ComputeStockAndShortage(
                Product p,
                Account? account,
                decimal totalKg,
                decimal totalUnits,
                HashSet<int> orderVariantIds,
                string displayMode)
        {
            var status = ProductCatalogStockClassification.ClassifyStock(p, account);

            if (displayMode == "status")
                return (null, null, null, null, status);

            var (sk, su) = ResolveCatalogStockDisplay(p, orderVariantIds);

            decimal? shK = null;
            decimal? shU = null;

            if (ShouldCompareDemandInUnits(p, totalKg, totalUnits))
            {
                var available = su ?? ResolveAvailableUnits(p, orderVariantIds);
                var d = totalUnits - available;
                shU = d > 0m ? Round2(d) : null;
            }
            else if (totalKg > 0m)
            {
                var available = sk ?? ResolveAvailableKg(p, orderVariantIds);
                var d = totalKg - available;
                shK = d > 0m ? Round2(d) : null;
            }
            else if (totalUnits > 0m)
            {
                var available = su ?? ResolveAvailableUnits(p, orderVariantIds);
                var d = totalUnits - available;
                shU = d > 0m ? Round2(d) : null;
            }

            return (sk, su, shK, shU, status);
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
