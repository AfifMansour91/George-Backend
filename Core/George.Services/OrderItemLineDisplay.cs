using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using George.DB;

namespace George.Services;

/// <summary>Optional display tweaks; keep in sync with TS <c>OrderItemAttributeDisplayOptions</c>.</summary>
public readonly record struct OrderItemAttributeDisplayOptions
{
    /// <summary>Omit <see cref="OrderItem.OrderLineSizeLabel"/> from attribute segments (voucher / compact surfaces).</summary>
    public bool OmitOrderLineSizeLabel { get; init; }

    /// <summary>Voucher: show per-unit weight only for "בחירת משקל ליחידה" lines.</summary>
    public bool VoucherPerUnitWeightVariableOnly { get; init; }
}

/// <summary>
/// Port of shop-manager <c>src/lib/orderItemLineDisplay.ts</c> for voucher / list parity (quantity badge, product name, attribute line).
/// Keep in sync when TS display rules change. See <c>docs/ORDER_ITEM_LINE_DISPLAY_PARITY.md</c> and <c>George.Services.Tests</c>.
/// </summary>
/// <remarks>
/// Picking/pricing helpers that require a live <c>Product</c> catalog (<c>resolveOrderItemPickingRatePerKg</c>, etc.) are intentionally not ported here.
/// </remarks>
public static class OrderItemLineDisplay
{
    private static string? NormMode(OrderItem item) =>
        string.IsNullOrWhiteSpace(item.OrderLineQuantityMode) ? null : item.OrderLineQuantityMode.Trim();

    /// <summary>Parse "200 גרם", "1.5 ק\"ג ליח'" etc. → grams (0 if unknown).</summary>
    public static int ParseGramsFromHebrewWeightLabel(string? label)
    {
        var s = (label ?? "").Trim();
        if (s.Length == 0) return 0;

        var kgM = Regex.Match(s, @"([\d.]+)\s*ק[״""\u0022']?\s*ג");
        if (kgM.Success && double.TryParse(kgM.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var kg))
        {
            if (double.IsFinite(kg) && kg > 0) return (int)Math.Round(kg * 1000);
        }

        var gM = Regex.Match(s, @"([\d.]+)\s*ג[ר]?ם");
        if (gM.Success && double.TryParse(gM.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var g))
        {
            if (double.IsFinite(g) && g > 0) return (int)Math.Round(g);
        }

        var gApo = Regex.Match(s, @"([\d.]+)\s*גר['׳`]");
        if (gApo.Success && double.TryParse(gApo.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ga))
        {
            if (double.IsFinite(ga) && ga > 0) return (int)Math.Round(ga);
        }

        return 0;
    }

    private static int ParseBareNumberSaleTotalToGrams(string? sIn)
    {
        var t = (sIn ?? "").Trim();
        if (t.Length == 0) return 0;
        if (ParseGramsFromHebrewWeightLabel(t) > 0) return 0;
        var m = Regex.Match(t, @"^([\d.]+)\s*$");
        if (!m.Success) return 0;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return 0;
        if (!double.IsFinite(n) || n <= 0) return 0;
        var raw = m.Groups[1].Value;
        if (raw.Contains('.')) return (int)Math.Round(n * 1000);
        if (n >= 50 && n <= 100000) return (int)Math.Round(n);
        if (n < 50) return (int)Math.Round(n * 1000);
        return 0;
    }

    private static int ReconcileParsedGramsToPerUnitForUnitsMode(int parsedGrams, OrderItem item)
    {
        var q = (double)item.Quantity;
        var p = item.PricePerUnit.HasValue ? (double)item.PricePerUnit.Value : 0;
        var t = item.TotalPrice.HasValue ? (double)item.TotalPrice.Value : 0;
        if (!(parsedGrams > 0 && q > 0 && p > 0 && t > 0)) return parsedGrams;

        var totalGramsFromPrice = t / p * 1000;
        if (!double.IsFinite(totalGramsFromPrice) || totalGramsFromPrice <= 0) return parsedGrams;

        var relTol = Math.Max(2, totalGramsFromPrice * 0.02);
        var impliedLineFromPerUnit = parsedGrams * q;

        if (Math.Abs(parsedGrams - totalGramsFromPrice) <= relTol)
            return (int)Math.Round(parsedGrams / q);
        if (Math.Abs(impliedLineFromPerUnit - totalGramsFromPrice) <= relTol)
            return parsedGrams;

        var errAsPerUnit = Math.Abs(impliedLineFromPerUnit - totalGramsFromPrice);
        var errAsLineTotal = Math.Abs(parsedGrams - totalGramsFromPrice);
        if (errAsLineTotal < errAsPerUnit) return (int)Math.Round(parsedGrams / q);
        return parsedGrams;
    }

    private static int InferGramsPerUnitFromUnitsLineEconomics(OrderItem item)
    {
        if (!string.Equals(NormMode(item), "units", StringComparison.OrdinalIgnoreCase)) return 0;
        var q = (double)item.Quantity;
        var p = item.PricePerUnit.HasValue ? (double)item.PricePerUnit.Value : 0;
        var t = item.TotalPrice.HasValue ? (double)item.TotalPrice.Value : 0;
        if (!(q > 0 && p > 0 && t > 0)) return 0;
        var hasHint = SaleUnitsIndicatesPieceSale(item.SaleUnits) || !string.IsNullOrWhiteSpace(item.SaleTotalWeight);
        if (!hasHint) return 0;
        var kg = t / (q * p);
        if (!double.IsFinite(kg) || kg <= 0 || kg > 500) return 0;
        return (int)Math.Round(kg * 1000);
    }

    /// <summary>Grams per catalog unit: DB field first, else snapshot label.</summary>
    public static int GramsPerUnitFromOrderItem(OrderItem item)
    {
        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        if (uw > 0) return (int)Math.Round(uw);

        var fromPer = ParseGramsFromHebrewWeightLabel(item.OrderLinePerUnitWeightLabel);
        if (fromPer > 0) return fromPer;
        var fromSize = ParseGramsFromHebrewWeightLabel(item.OrderLineSizeLabel);
        if (fromSize > 0) return fromSize;

        if (string.Equals(NormMode(item), "units", StringComparison.OrdinalIgnoreCase))
        {
            var st = item.SaleTotalWeight?.Trim();
            if (!string.IsNullOrEmpty(st))
            {
                var fromSale = ParseGramsFromHebrewWeightLabel(st);
                if (fromSale > 0) return ReconcileParsedGramsToPerUnitForUnitsMode(fromSale, item);
                var bare = ParseBareNumberSaleTotalToGrams(st);
                if (bare > 0) return ReconcileParsedGramsToPerUnitForUnitsMode(bare, item);
            }
            var econ = InferGramsPerUnitFromUnitsLineEconomics(item);
            if (econ > 0) return econ;
        }

        return 0;
    }

    private static double TotalGramsLegacy(OrderItem item)
    {
        var q = (double)item.Quantity;
        var g = GramsPerUnitFromOrderItem(item);
        if (g <= 0) return 0;
        return q * g;
    }

    public static string FormatTotalGramsHebrew(double totalGrams)
    {
        if (totalGrams <= 0) return "";
        if (totalGrams >= 1000)
        {
            // Match TS: totalGrams % 1000 === 0 ? totalGrams/1000 : parseFloat((totalGrams/1000).toFixed(1))
            var rem = Math.Abs(totalGrams % 1000);
            if (rem < 1e-6)
            {
                var exactKg = totalGrams / 1000.0;
                var s = exactKg.ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
                return $"{s} ק\"ג";
            }
            var kgOneDec = Math.Round(totalGrams / 1000.0 * 10) / 10.0;
            return $"{kgOneDec.ToString("0.#", CultureInfo.InvariantCulture)} ק\"ג";
        }
        return $"{Math.Round(totalGrams)} גרם";
    }

    private static bool SaleUnitsIndicatesPieceSale(string? saleUnits)
    {
        var s = saleUnits?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s.Contains("יח", StringComparison.Ordinal)) return true;
        if (Regex.IsMatch(s, @"\b(pcs?|pieces?|ea\.?)\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(s, @"\d\s*[×x]|[×x]\s*\d")) return true;
        return false;
    }

    private static bool SaleTotalWeightIndicatesOrderTotalKg(string? saleTotalWeight)
    {
        var s = saleTotalWeight?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        return Regex.IsMatch(s, @"ק\s*[""״']?\s*ג") || Regex.IsMatch(s, @"\bקג\b", RegexOptions.IgnoreCase);
    }

    private static string FormatPieceCountBadge(double q)
    {
        var whole = Math.Abs(q - Math.Round(q)) < 0.001;
        var n = whole ? Math.Round(q) : Math.Round(q * 100) / 100.0;
        return $"{n.ToString(whole ? "0" : "0.##", CultureInfo.InvariantCulture)} יח'";
    }

    public static bool IsOrderItemPickedByWeight(OrderItem item)
    {
        var mode = NormMode(item);
        var g = GramsPerUnitFromOrderItem(item);

        if (string.Equals(mode, "weight", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(mode, "units", StringComparison.OrdinalIgnoreCase)) return g > 0;

        if (SaleTotalWeightIndicatesOrderTotalKg(item.SaleTotalWeight)) return true;
        if (SaleUnitsIndicatesPieceSale(item.SaleUnits) && g > 0) return true;

        if (!string.IsNullOrWhiteSpace(item.OrderLinePerUnitWeightLabel) || !string.IsNullOrWhiteSpace(item.OrderLineSizeLabel))
            return g > 0;

        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        return uw > 0;
    }

    public static bool IsOrderItemWeightedSoldByUnits(OrderItem item)
    {
        if (string.Equals(NormMode(item), "weight", StringComparison.OrdinalIgnoreCase)) return false;

        var g = GramsPerUnitFromOrderItem(item);
        if (g <= 0) return false;

        if (string.Equals(NormMode(item), "units", StringComparison.OrdinalIgnoreCase)) return true;

        if (NormMode(item) != null) return false;

        if (SaleUnitsIndicatesPieceSale(item.SaleUnits)) return true;
        if (!string.IsNullOrWhiteSpace(item.OrderLinePerUnitWeightLabel)) return true;

        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        if (uw <= 0) return false;

        var q = (double)item.Quantity;
        var qInt = Math.Abs(q - Math.Round(q)) < 0.001 ? (int)Math.Round(q) : (int?)null;

        if (qInt is >= 2) return true;

        if (qInt == 1)
        {
            if (SaleTotalWeightIndicatesOrderTotalKg(item.SaleTotalWeight) && !SaleUnitsIndicatesPieceSale(item.SaleUnits))
                return false;
            return true;
        }

        return false;
    }

    public static string OrderItemAttrDedupeKey(string s)
    {
        var sb = new StringBuilder(s.Normalize(NormalizationForm.FormKC));
        sb.Replace('\u201C', '"').Replace('\u201D', '"').Replace('\u201E', '"').Replace('״', '"');
        sb.Replace('׳', '\'');
        var compact = Regex.Replace(sb.ToString(), @"\s+", " ").Trim().ToLowerInvariant();
        return compact;
    }

    private static List<string> DedupeAttributeStringsPreservingOrder(IEnumerable<string> parts)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var outList = new List<string>();
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length == 0) continue;
            var k = OrderItemAttrDedupeKey(t);
            if (keys.Contains(k)) continue;
            keys.Add(k);
            outList.Add(t);
        }
        return outList;
    }

    public static string FormatPickedLineTotalWeightHebrew(double kg)
    {
        if (!double.IsFinite(kg) || kg < 0) return "";
        if (kg == 0) return "0 גרם";
        if (kg >= 1)
        {
            var isWhole = Math.Abs(kg % 1) < 0.0001;
            return isWhole
                ? $"{Math.Round(kg)} ק\"ג"
                : $"{double.Round(kg, 3).ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.')} ק\"ג";
        }
        if (kg >= 0.001)
        {
            var s = kg.ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            return $"{s} ק\"ג";
        }
        return $"{Math.Round(kg * 1000)} גרם";
    }

    public static bool IsGenericVariantTitle(string vt)
    {
        var s = vt.Trim().Normalize(NormalizationForm.FormC);
        if (s.Length == 0) return true;
        if (string.Equals(s, "יחידה", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(s, @"^kg$", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(s, @"^ק[״""\u0022']?\s*ג$", RegexOptions.IgnoreCase)) return true;
        if (s.Length <= 4 && s.Contains('ק') && s.Contains('ג')) return true;
        return false;
    }

    private static string SanitizePerUnitWeightLabelIfGramsShownAsKg(OrderItem item, string label)
    {
        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        if (!(uw >= 1000 && label.Contains("ליח", StringComparison.Ordinal))) return label;
        var m = Regex.Match(label, @"^([\d.]+)\s*ק[״""\u0022']?\s*ג\s*ליח");
        if (!m.Success) return label;
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var shownKg)) return label;
        var intPart = m.Groups[1].Value.Split('.')[0];
        if (intPart.Length < 4) return label;
        if (Math.Abs(shownKg - uw) < 1) return FormatPerUnitHebrewFromGrams((int)Math.Round(uw));
        return label;
    }

    private static bool TitleIndicatesSoldByWeightChannelOnly(string? title)
    {
        var t = (title ?? "").Trim();
        if (!Regex.IsMatch(t, @"מכירה\s+במשקל", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(t, @"מכירה\s+במשקל\s*\+", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(t, @"לפי\s+יחידה", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(t, @"משקל\s+לפי\s+גודל", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(t, @"בחירת\s+משקל\s+ליחידה", RegexOptions.IgnoreCase)) return false;
        return true;
    }

    /// <summary>Primary quantity/weight badge (same as TS formatOrderItemQuantityBadge).</summary>
    public static string FormatOrderItemQuantityBadge(OrderItem item)
    {
        var mode = NormMode(item);
        var q = (double)item.Quantity;
        var qtyWhole = Math.Abs(q - Math.Round(q)) < 0.001;
        int? qInt = qtyWhole ? (int)Math.Round(q) : null;
        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;

        if (string.Equals(mode, "units", StringComparison.OrdinalIgnoreCase))
            return FormatPieceCountBadge(q);

        if (string.Equals(mode, "weight", StringComparison.OrdinalIgnoreCase))
        {
            var st = item.SaleTotalWeight?.Trim();
            if (!string.IsNullOrEmpty(st)) return st;
            var totalG = TotalGramsLegacy(item);
            if (totalG > 0) return FormatTotalGramsHebrew(totalG);
            return FormatPieceCountBadge(q);
        }

        if (SaleUnitsIndicatesPieceSale(item.SaleUnits))
            return item.SaleUnits!.Trim();

        if (qInt is >= 2 && uw > 0)
        {
            if (TitleIndicatesSoldByWeightChannelOnly(item.Title))
            {
                var tg = TotalGramsLegacy(item);
                if (tg > 0) return FormatTotalGramsHebrew(tg);
            }
            return $"{qInt} יח'";
        }

        if (!string.IsNullOrWhiteSpace(item.OrderLinePerUnitWeightLabel) || !string.IsNullOrWhiteSpace(item.OrderLineSizeLabel))
            return FormatPieceCountBadge(q);

        if (!string.IsNullOrWhiteSpace(item.SaleTotalWeight) && qInt == 1 && !SaleUnitsIndicatesPieceSale(item.SaleUnits))
            return item.SaleTotalWeight.Trim();

        var totalGrams = TotalGramsLegacy(item);
        var preferUnitsDisplay =
            item.UnitWeightGrams != null &&
            uw > 0 &&
            qtyWhole &&
            qInt == 1 &&
            uw < 1000 &&
            !TitleIndicatesSoldByWeightChannelOnly(item.Title);

        if (!preferUnitsDisplay && item.UnitWeightGrams != null && totalGrams > 0)
            return FormatTotalGramsHebrew(totalGrams);

        return FormatPieceCountBadge(q);
    }

    public static string FormatPerUnitHebrewFromGrams(double grams)
    {
        if (!double.IsFinite(grams) || grams <= 0) return "";
        if (grams < 1000) return $"{Math.Round(grams)} גרם ליח'";
        var kg = grams / 1000.0;
        var s = Math.Abs(kg - Math.Round(kg)) < 0.001
            ? Math.Round(kg).ToString("0", CultureInfo.InvariantCulture)
            : double.Round(kg, 2).ToString("0.##", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        return $"{s} ק\"ג ליח'";
    }

    private static string? InferredPerUnitWeightLabel(OrderItem item)
    {
        if (string.Equals(NormMode(item), "weight", StringComparison.OrdinalIgnoreCase)) return null;

        var g = GramsPerUnitFromOrderItem(item);
        if (g <= 0) return null;

        if (IsOrderItemWeightedSoldByUnits(item))
            return FormatPerUnitHebrewFromGrams(g);

        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        if (uw <= 0) return null;

        var mode = NormMode(item);
        var q = (double)item.Quantity;
        var qInt = Math.Abs(q - Math.Round(q)) < 0.001 ? (int)Math.Round(q) : (int?)null;

        if (string.Equals(mode, "units", StringComparison.OrdinalIgnoreCase))
            return FormatPerUnitHebrewFromGrams(g);

        if (mode != null) return null;

        if (SaleUnitsIndicatesPieceSale(item.SaleUnits))
            return FormatPerUnitHebrewFromGrams(g);

        if (qInt is >= 2)
        {
            if (TitleIndicatesSoldByWeightChannelOnly(item.Title)) return null;
            return FormatPerUnitHebrewFromGrams(g);
        }

        if (!string.IsNullOrWhiteSpace(item.OrderLineSizeLabel))
            return FormatPerUnitHebrewFromGrams(g);

        var preferUnits =
            qInt == 1 &&
            uw > 0 &&
            uw < 1000 &&
            !TitleIndicatesSoldByWeightChannelOnly(item.Title);
        if (preferUnits) return FormatPerUnitHebrewFromGrams(g);

        if (qInt == 1 && uw >= 1000)
        {
            var t = item.Title ?? "";
            if (Regex.IsMatch(t, @"לפי\s+יחידה|משקל\s+לפי\s+גודל|מכירה\s+במשקל\s*\+", RegexOptions.IgnoreCase))
                return FormatPerUnitHebrewFromGrams(g);
        }

        return null;
    }

    public static IReadOnlyList<string> GetOrderItemAttributeSegments(
        OrderItem item,
        OrderItemAttributeDisplayOptions options = default)
    {
        var size = options.OmitOrderLineSizeLabel ? null : item.OrderLineSizeLabel?.Trim();
        var perRaw = item.OrderLinePerUnitWeightLabel?.Trim() ?? InferredPerUnitWeightLabel(item);
        var per = !string.IsNullOrEmpty(perRaw) ? SanitizePerUnitWeightLabelIfGramsShownAsKg(item, perRaw) : null;
        if (options.VoucherPerUnitWeightVariableOnly && !string.IsNullOrEmpty(per) && !IsOrderItemVariableWeightPerUnitChoice(item))
            per = null;
        var cut = item.OrderLineCuttingLabel?.Trim();
        var title = item.Title?.Trim() ?? "";
        var vtRaw = item.VariantTitle?.Trim();

        if (string.IsNullOrEmpty(cut) && !string.IsNullOrEmpty(vtRaw) && !IsGenericVariantTitle(vtRaw) && vtRaw != title)
        {
            var mode = NormMode(item);
            if (string.Equals(mode, "weight", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "units", StringComparison.OrdinalIgnoreCase))
            {
                cut = vtRaw;
            }
            else if (mode == null && !string.IsNullOrEmpty(per))
            {
                cut = vtRaw;
            }
            else if (mode == null)
            {
                var q = (double)item.Quantity;
                var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
                var looksSoldByTotalWeight =
                    !string.IsNullOrWhiteSpace(item.SaleTotalWeight) &&
                    !SaleUnitsIndicatesPieceSale(item.SaleUnits) &&
                    Math.Abs(q - 1) < 0.001 &&
                    uw >= 1000;
                if (looksSoldByTotalWeight) cut = vtRaw;
            }
        }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(size)) parts.Add(size);
        if (!string.IsNullOrEmpty(per)) parts.Add(per);
        if (!string.IsNullOrEmpty(cut)) parts.Add(cut);
        var structured = DedupeAttributeStringsPreservingOrder(parts);
        if (structured.Count > 0) return structured;

        if (!string.IsNullOrEmpty(vtRaw) && vtRaw != title && !IsGenericVariantTitle(vtRaw))
            return new[] { vtRaw };
        return Array.Empty<string>();
    }

    public static string GetOrderItemProductName(OrderItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Title)) return item.Title.Trim();
        if (!string.IsNullOrWhiteSpace(item.VariantTitle)) return item.VariantTitle.Trim();
        return "–";
    }

    public static string? GetOrderItemAttributeSummaryLine(
        OrderItem item,
        OrderItemAttributeDisplayOptions options = default)
    {
        var segs = GetOrderItemAttributeSegments(item, options);
        var line = string.Join(" | ", segs).Trim();
        return line.Length > 0 ? line : null;
    }

    /// <summary>Matches TS <c>orderItemIsPickedForUi</c> — ליקוט אמיתי בלבד, לא baseline מלאי.</summary>
    public static bool OrderItemIsPickedForUi(OrderItem item) => item.PickingUserConfirmed;

    /// <summary>Matches TS <c>orderMeaningfulPick</c> — שורת "לוקט" בבון רק אחרי אישור ליקוט.</summary>
    public static bool OrderMeaningfulPick(OrderItem item) => OrderItemIsPickedForUi(item);

    /// <summary>True when ליקוט changed billing: at least one confirmed pick, or a line explicitly zeroed (unpicked).</summary>
    public static bool OrderHasOcStoreosPickingAdjustments(IEnumerable<OrderItem> items)
    {
        var active = items.Where(i => !i.IsDeleted).ToList();
        if (active.Any(i => i.PickingUserConfirmed))
            return true;
        return active.Any(i => i.PickedQuantity is 0m && !i.TotalPrice.HasValue);
    }

    /// <summary>oc-storeos POST: bill only lines confirmed in picking with qty &gt; 0.</summary>
    public static bool IsOcStoreosBillableLine(OrderItem item) =>
        item.PickingUserConfirmed && item.PickedQuantity is > 0m;

    /// <summary>Line total for oc-storeos after picking; null when the line must not be charged.</summary>
    public static decimal? GetOcStoreosBillableLineTotal(OrderItem item)
    {
        if (!IsOcStoreosBillableLine(item)) return null;
        if (item.TotalPrice.HasValue) return item.TotalPrice.Value;
        return item.PickedQuantity!.Value * (item.PricePerUnit ?? 0m);
    }

    /// <summary>Matches TS voucher <c>formatPickedQuantity</c> (unitWeightGrams &gt; 0 ⇒ kg).</summary>
    public static string FormatVoucherPickedDisplay(OrderItem item)
    {
        var q = item.PickedQuantity.HasValue ? (double)item.PickedQuantity.Value : 0;
        var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
        if (uw > 0)
        {
            var s = Math.Abs(q % 1) < 0.0001
                ? q.ToString("0", CultureInfo.InvariantCulture)
                : q.ToString("0.###", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            return $"{s} ק\"ג";
        }
        return FormatPieceCountBadge(q);
    }

    /// <summary>מוצר "בחירת משקל ליחידה" — לפי כותרת שורה (בון ללא קטלוג).</summary>
    public static bool IsOrderItemVariableWeightPerUnitChoice(OrderItem item)
    {
        var title = item.Title ?? "";
        if (Regex.IsMatch(title, @"בחירת\s*משקל\s*ליחידה", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(title, @"בחר\s*משקל\s*יח", RegexOptions.IgnoreCase)) return true;
        // Line-field signature: only the by_unit+variable snapshot builder writes a per-unit weight
        // label WITHOUT UnitWeightGrams (fixed/by_variant/by_unit_and_weight all carry grams), so a
        // units-mode line matching it is a "בחר משקל ליח'" choice even when the title lacks the phrase.
        if (string.Equals(item.OrderLineQuantityMode?.Trim(), "units", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.OrderLinePerUnitWeightLabel)
            && !(item.UnitWeightGrams is > 0))
        {
            return true;
        }
        return false;
    }

    private static double ResolveWeightModeOrderedKgFromLine(OrderItem item)
    {
        var st = item.SaleTotalWeight?.Trim();
        if (!string.IsNullOrEmpty(st))
        {
            var grams = ParseGramsFromHebrewWeightLabel(st);
            if (grams > 0) return grams / 1000.0;
            var bare = ParseBareNumberSaleTotalToGrams(st);
            if (bare > 0) return bare / 1000.0;
        }
        var q = (double)item.Quantity;
        if (q > 0 && q < 500) return q;
        return 0;
    }

    /// <summary>משקל מוזמן משוער (ק"ג) — לתמחור ₪/ק"ג בבון.</summary>
    public static double GetOrderLineOrderedWeightKg(OrderItem item)
    {
        var mode = NormMode(item);
        var q = (double)item.Quantity;
        if (string.Equals(mode, "weight", StringComparison.OrdinalIgnoreCase))
            return ResolveWeightModeOrderedKgFromLine(item);
        if (IsOrderItemWeightedSoldByUnits(item))
        {
            var pu = GramsPerUnitFromOrderItem(item);
            if (pu > 0 && q > 0) return q * pu / 1000.0;
            var totalG = TotalGramsLegacy(item);
            return totalG > 0 ? totalG / 1000.0 : 0;
        }
        if (mode == null && IsOrderItemPickedByWeight(item))
        {
            var st = item.SaleTotalWeight?.Trim();
            if (!string.IsNullOrEmpty(st))
            {
                var grams = ParseGramsFromHebrewWeightLabel(st);
                if (grams > 0) return grams / 1000.0;
            }
            var g = GramsPerUnitFromOrderItem(item);
            if (g > 0 && q > 0) return q * g / 1000.0;
        }
        return 0;
    }

    /// <summary>מחיר לק"ג לחישוב/תצוגה בבון (ללא קטלוג).</summary>
    public static decimal? GetOrderItemPricePerKgNumeric(OrderItem item)
    {
        var mode = NormMode(item);
        if (!item.PricePerUnit.HasValue) return null;
        var n = item.PricePerUnit.Value;
        if (string.Equals(mode, "weight", StringComparison.OrdinalIgnoreCase))
            return n;
        var g = GramsPerUnitFromOrderItem(item);
        if (g > 0) return n * 1000m / g;
        if (mode == null && !SaleUnitsIndicatesPieceSale(item.SaleUnits))
        {
            var uw = item.UnitWeightGrams.HasValue ? (double)item.UnitWeightGrams.Value : 0;
            if (uw > 0) return n;
        }
        if (IsOrderItemPickedByWeight(item) && item.TotalPrice.HasValue && item.TotalPrice.Value > 0)
        {
            var wKg = GetOrderLineOrderedWeightKg(item);
            if (wKg > 0) return item.TotalPrice.Value / (decimal)wKg;
        }
        return null;
    }

    /// <summary>תצוגת מחיר לעמודת "מחיר" בבון אחרי ליקוט.</summary>
    public static string? FormatOrderLinePricePerKgForPicking(OrderItem item)
    {
        if (IsOrderItemPickedByWeight(item))
        {
            var perKg = GetOrderItemPricePerKgNumeric(item);
            if (perKg.HasValue)
                return $"₪{perKg.Value.ToString("0.00", CultureInfo.InvariantCulture)} / ק\"ג";
        }
        if (!IsOrderItemPickedByWeight(item) && item.PricePerUnit.HasValue)
            return $"₪{item.PricePerUnit.Value.ToString("0.00", CultureInfo.InvariantCulture)} / יח'";
        if (item.PricePerUnit.HasValue)
            return $"₪{item.PricePerUnit.Value.ToString("0.00", CultureInfo.InvariantCulture)} / יח'";
        return null;
    }

    /// <summary>בון לפני ליקוט: משקל יחידה רק ל"בחירת משקל ליחידה".</summary>
    public static string? FormatVoucherLegacyUnitWeightHint(OrderItem item, bool newVoucher)
    {
        if (OrderItemIsPickedForUi(item)) return null;
        if (!IsOrderItemVariableWeightPerUnitChoice(item)) return null;

        var per = item.OrderLinePerUnitWeightLabel?.Trim();
        if (!string.IsNullOrEmpty(per))
        {
            var q = (double)item.Quantity;
            var qInt = Math.Abs(q - Math.Round(q)) < 0.001 ? (int)Math.Round(q) : (int?)null;
            var perClean = Regex.Replace(per, @"^\s*~\s*", "");
            if (qInt is > 1) return $"כל יחידה {perClean} · {qInt} יח׳";
            return per;
        }

        var g = GramsPerUnitFromOrderItem(item);
        if (g <= 0) return null;
        var qty = (double)item.Quantity;
        var qtyInt = Math.Abs(qty - Math.Round(qty)) < 0.001 ? (int)Math.Round(qty) : (int?)null;
        var kg = g / 1000.0;
        var kgText = g >= 1000 ? kg.ToString("0.0", CultureInfo.InvariantCulture) : kg.ToString("0.00", CultureInfo.InvariantCulture);
        if (qtyInt is > 1) return $"כל יחידה {kgText} ק״ג · {qtyInt} יח׳";
        return $"משקל יחידה: {kg.ToString("0.00", CultureInfo.InvariantCulture)} ק״ג";
    }
}
