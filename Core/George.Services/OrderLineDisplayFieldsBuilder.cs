using System.Globalization;
using System.Text.RegularExpressions;
using George.DB;
using George.Services.Request;

namespace George.Services;

/// <summary>
/// Mirrors shop-manager <c>orderLineWeightSnapshots.ts</c> / <c>buildOrderLineDisplayFieldsForCreate</c> for server-side persistence
/// when clients omit <c>orderLine*</c> fields (or for WooCommerce ingest).
/// </summary>
public static class OrderLineDisplayFieldsBuilder
{
    private static readonly Regex LeadingNumberRegex = new(@"^\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static readonly Regex CuttingKeyHint = new(@"צורת חיתוך|חיתוך|cutting", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void MergeComputedDisplayFields(OrderItem item, CreateOrderItemReq req, Product? product)
    {
        if (req.ProductId is null or <= 0) return;

        if (product == null)
        {
            ApplyHeuristicOrderLineDisplayWhenCatalogMissing(item, req);
            return;
        }

        var variantIndex = GetVariantIndex(product, req.ProductVariantId);
        var purchaseAsKgLine = InferPurchaseAsKgLine(product, req);
        var variableKg = InferVariableWeightKg(product, purchaseAsKgLine, req);
        var variantWeightKg = GetVariantWeightKg(product, variantIndex, req);

        // Woo lines never send OrderLineCuttingLabel — derive it from the resolved variant's cutting
        // option so prints/reports render like phone orders (size label + cutting, not the raw variantTitle).
        var cuttingValue = string.IsNullOrWhiteSpace(req.OrderLineCuttingLabel)
            ? GetVariantCuttingValue(product, variantIndex)
            : req.OrderLineCuttingLabel;

        var snap = Build(product, purchaseAsKgLine, req.Quantity, variantIndex, variantWeightKg, cuttingValue, variableKg);

        if (string.IsNullOrWhiteSpace(req.OrderLineQuantityMode) && !string.IsNullOrWhiteSpace(snap.OrderLineQuantityMode))
            item.OrderLineQuantityMode = snap.OrderLineQuantityMode;

        if (string.IsNullOrWhiteSpace(req.OrderLinePerUnitWeightLabel) && !string.IsNullOrWhiteSpace(snap.OrderLinePerUnitWeightLabel))
            item.OrderLinePerUnitWeightLabel = snap.OrderLinePerUnitWeightLabel;

        if (string.IsNullOrWhiteSpace(req.OrderLineSizeLabel) && !string.IsNullOrWhiteSpace(snap.OrderLineSizeLabel))
            item.OrderLineSizeLabel = snap.OrderLineSizeLabel;

        if (string.IsNullOrWhiteSpace(req.OrderLineCuttingLabel) && !string.IsNullOrWhiteSpace(snap.OrderLineCuttingLabel))
            item.OrderLineCuttingLabel = snap.OrderLineCuttingLabel;

        if (!req.UnitWeightGrams.HasValue && snap.UnitWeightGrams.HasValue)
            item.UnitWeightGrams = snap.UnitWeightGrams;

        if (string.IsNullOrWhiteSpace(req.SaleUnits) && !string.IsNullOrWhiteSpace(snap.SaleUnits))
            item.SaleUnits = snap.SaleUnits;

        if (string.IsNullOrWhiteSpace(req.SaleTotalWeight) && !string.IsNullOrWhiteSpace(snap.SaleTotalWeight))
            item.SaleTotalWeight = snap.SaleTotalWeight;

        if (snap.ClearSaleTotalWeight && string.IsNullOrWhiteSpace(req.SaleTotalWeight)
            && string.IsNullOrWhiteSpace(req.OrderLineQuantityMode))
            item.SaleTotalWeight = null;

        // Typed display snapshot (LineDisplayJson) — written unconditionally for catalog lines so
        // structured rendering (Site.UseStructuredOrderLineDisplay) can take over without re-parsing labels.
        item.LineDisplayJson = BuildLineDisplaySnapshot(product, req, purchaseAsKgLine, variantIndex, variantWeightKg, cuttingValue, variableKg)?.ToJson();
    }

    /// <summary>Typed snapshot from catalog + request — numbers and clean names only (writer-side normalization).</summary>
    private static OrderLineDisplaySnapshot? BuildLineDisplaySnapshot(
        Product product,
        CreateOrderItemReq req,
        bool purchaseAsKgLine,
        int variantIndex,
        decimal? variantWeightKg,
        string? cuttingValue,
        decimal? variableKg)
    {
        var state = GetPricingState(product);

        var kind = OrderLineDisplayKinds.Standard;
        if (state.IsWeighted)
        {
            if (string.Equals(state.SetupType, "by_weight", StringComparison.OrdinalIgnoreCase))
                kind = OrderLineDisplayKinds.ByWeight;
            else if (string.Equals(state.SetupType, "by_unit_and_weight", StringComparison.OrdinalIgnoreCase))
                kind = OrderLineDisplayKinds.ByUnitAndWeight;
            else if (string.Equals(state.SetupType, "by_unit", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(state.UnitWeightMode, "variable", StringComparison.OrdinalIgnoreCase))
                    kind = OrderLineDisplayKinds.ByUnitVariable;
                else if (string.Equals(state.UnitWeightMode, "by_variant", StringComparison.OrdinalIgnoreCase) || state.WeightByVariant)
                    kind = OrderLineDisplayKinds.ByUnitByVariant;
                else
                    kind = OrderLineDisplayKinds.ByUnitAverage;
            }
        }

        int? approxGrams = null;
        if (kind == OrderLineDisplayKinds.ByUnitByVariant)
        {
            var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
            var wKg = variantWeightKg is > 0 ? variantWeightKg.Value : (v?.Weight ?? (decimal)state.UnitWeight);
            var g = ToGramsFromConfigWeight(wKg, state.UnitRaw);
            if (g > 0) approxGrams = decimal.ToInt32(g);
        }
        else if (kind is OrderLineDisplayKinds.ByUnitAverage or OrderLineDisplayKinds.ByUnitAndWeight)
        {
            var g = ToGramsFromConfigWeight(state.UnitWeight, state.UnitRaw);
            if (g > 0) approxGrams = decimal.ToInt32(g);
        }

        int? chosenGrams = null;
        if (kind == OrderLineDisplayKinds.ByUnitVariable)
        {
            if (variableKg is > 0)
            {
                var g = ToGramsFromConfigWeight(variableKg.Value, "kg");
                if (g > 0) chosenGrams = decimal.ToInt32(g);
            }
            else
            {
                // The chosen weight arrives differently per channel — normalize it to grams HERE
                // (single writer) so renderers never parse text: manual/kiosk send a Hebrew label,
                // Woo sends unitWeight grams (1000 = by-kg sentinel, not a portion) or a bare-kg saleTotalWeight.
                var g = OrderItemLineDisplay.ParseGramsFromHebrewWeightLabel(req.OrderLinePerUnitWeightLabel);
                if (g <= 0 && req.UnitWeightGrams is > 0 && req.UnitWeightGrams != 1000m)
                    g = decimal.ToInt32(decimal.Round(req.UnitWeightGrams.Value, 0, MidpointRounding.AwayFromZero));
                if (g <= 0)
                {
                    var kgFromSale = TryParseLeadingDecimalKg(req.SaleTotalWeight);
                    if (kgFromSale is > 0 and < 500)
                        g = decimal.ToInt32(decimal.Round(kgFromSale.Value * 1000m, 0, MidpointRounding.AwayFromZero));
                }
                if (g > 0) chosenGrams = g;
            }
        }

        var sizeName = GetVariantSizeName(product, variantIndex);

        decimal? unitCount = null;
        int? totalGrams = null;
        if (purchaseAsKgLine || kind == OrderLineDisplayKinds.ByWeight)
        {
            var totalKg = req.Quantity;
            if (totalKg > 0) totalGrams = decimal.ToInt32(decimal.Round(totalKg * 1000m, 0, MidpointRounding.AwayFromZero));
        }
        else
        {
            unitCount = req.Quantity;
            var perUnit = chosenGrams ?? approxGrams;
            if (perUnit is > 0 && req.Quantity > 0)
                totalGrams = decimal.ToInt32(decimal.Round(perUnit.Value * req.Quantity, 0, MidpointRounding.AwayFromZero));
        }

        return new OrderLineDisplaySnapshot
        {
            Kind = kind,
            SizeName = string.IsNullOrWhiteSpace(sizeName) ? null : sizeName,
            ApproxUnitWeightGrams = approxGrams,
            ChosenUnitWeightGrams = chosenGrams,
            CuttingName = string.IsNullOrWhiteSpace(cuttingValue) ? null : cuttingValue.Trim(),
            UnitCount = unitCount,
            TotalWeightGrams = totalGrams,
        };
    }

    /// <summary>Clean size name from the resolved variant (non-cutting option; prefers "גודל"/"size").</summary>
    private static string? GetVariantSizeName(Product product, int variantIndex)
    {
        var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
        var ov = v?.ProductVariantOptionValue;
        if (ov == null) return null;
        string? firstNonCutting = null;
        foreach (var kv in ov)
        {
            if (string.IsNullOrWhiteSpace(kv.OptionValue)) continue;
            var name = kv.OptionName ?? "";
            if (name == "גודל" || string.Equals(name, "size", StringComparison.OrdinalIgnoreCase))
                return kv.OptionValue.Trim();
            if (firstNonCutting == null && !CuttingKeyHint.IsMatch(name))
                firstNonCutting = kv.OptionValue.Trim();
        }
        return firstNonCutting;
    }

    /// <summary>
    /// When the catalog product cannot be loaded (e.g. <c>ProductId</c> still Woo id, or sync gap), still set
    /// <see cref="OrderItem.OrderLineQuantityMode"/> so the shop UI is not stuck in legacy-only inference.
    /// </summary>
    private static void ApplyHeuristicOrderLineDisplayWhenCatalogMissing(OrderItem item, CreateOrderItemReq req)
    {
        if (!string.IsNullOrWhiteSpace(item.OrderLineQuantityMode)) return;

        if (req.UnitWeightGrams == 1000m)
        {
            item.OrderLineQuantityMode = "weight";
            return;
        }

        var su = req.SaleUnits;
        if (!string.IsNullOrWhiteSpace(su) && su.Contains("יח", StringComparison.Ordinal))
        {
            item.OrderLineQuantityMode = "units";
            return;
        }

        if (!string.IsNullOrWhiteSpace(req.SaleTotalWeight))
        {
            item.OrderLineQuantityMode = "weight";
            return;
        }

        item.OrderLineQuantityMode = "units";
    }

    private static bool InferPurchaseAsKgLine(Product product, CreateOrderItemReq req)
    {
        if (req.UnitWeightGrams == 1000m)
            return true;

        var setup = product.SetupType?.Name ?? "";
        var wc = product.WeightConfig;
        var unitWeightMode = wc?.UnitWeightMode?.Name ?? "";

        if (string.Equals(setup, "by_unit", StringComparison.OrdinalIgnoreCase)
            && string.Equals(unitWeightMode, "variable", StringComparison.OrdinalIgnoreCase))
        {
            if (req.Quantity != decimal.Truncate(req.Quantity))
                return true;
            var wKg = TryParseLeadingDecimalKg(req.SaleTotalWeight);
            if (wKg.HasValue && Math.Abs(wKg.Value - req.Quantity) < 0.05m)
                return true;
            return false;
        }

        if (string.Equals(setup, "by_weight", StringComparison.OrdinalIgnoreCase))
        {
            var su = TryParseSaleUnitsCount(req.SaleUnits);
            if (su.HasValue && Math.Abs(su.Value - req.Quantity) < 0.001m)
                return false;
            if (req.Quantity != decimal.Truncate(req.Quantity))
                return true;
            return false;
        }

        return false;
    }

    private static decimal? InferVariableWeightKg(Product product, bool purchaseAsKgLine, CreateOrderItemReq req)
    {
        var setup = product.SetupType?.Name ?? "";
        var wc = product.WeightConfig;
        var unitWeightMode = wc?.UnitWeightMode?.Name ?? "";
        if (!string.Equals(setup, "by_unit", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(unitWeightMode, "variable", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!purchaseAsKgLine)
            return null;
        return req.Quantity > 0 ? req.Quantity : null;
    }

    private static decimal? GetVariantWeightKg(Product product, int variantIndex, CreateOrderItemReq req)
    {
        // UnitWeightGrams on the wire is always grams (kiosk / Woo / API); do not treat as kg when catalog unit is kg.
        if (req.UnitWeightGrams is { } g && g > 0 && g != 1000m)
            return g / 1000m;

        var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
        return v?.Weight;
    }

    /// <summary>Cutting-like option value ("צורת חיתוך") from the resolved variant row, if any.</summary>
    private static string? GetVariantCuttingValue(Product product, int variantIndex)
    {
        var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
        var ov = v?.ProductVariantOptionValue;
        if (ov == null) return null;
        foreach (var kv in ov)
        {
            if (!string.IsNullOrWhiteSpace(kv.OptionValue) && CuttingKeyHint.IsMatch(kv.OptionName ?? ""))
                return kv.OptionValue.Trim();
        }
        return null;
    }

    public static int GetVariantIndex(Product product, int? productVariantId)
    {
        var list = GetOrderedVariants(product);
        if (!productVariantId.HasValue || list.Count == 0) return 0;
        var idx = list.FindIndex(v => v.Id == productVariantId.Value);
        return idx >= 0 ? idx : 0;
    }

    private static List<ProductVariant> GetOrderedVariants(Product product) =>
        product.ProductVariant?.Where(v => !v.IsDeleted).OrderBy(v => v.Id).ToList() ?? new List<ProductVariant>();

    private static DisplayFieldsSnapshot Build(
        Product product,
        bool purchaseAsKgLine,
        decimal quantity,
        int variantIndex,
        decimal? variantWeightKg,
        string? cuttingValue,
        decimal? variableWeightKg)
    {
        var cutting = cuttingValue?.Trim();
        var state = GetPricingState(product);

        if (!state.IsWeighted)
            return AsUnits(new DisplayFieldsSnapshot(), cutting);

        if (purchaseAsKgLine)
        {
            string? perUnit = null;
            if (string.Equals(state.SetupType, "by_unit", StringComparison.OrdinalIgnoreCase)
                && string.Equals(state.UnitWeightMode, "variable", StringComparison.OrdinalIgnoreCase)
                && variableWeightKg is > 0)
            {
                var g = ToGramsFromConfigWeight(variableWeightKg.Value, state.UnitRaw);
                if (g > 0) perUnit = FormatPerUnitWeightHebrew(g, state.UnitRaw);
            }

            var saleTotalWeight = FormatSaleTotalWeightFromTotalKg(quantity);
            return new DisplayFieldsSnapshot
            {
                OrderLineQuantityMode = "weight",
                OrderLineCuttingLabel = cutting,
                OrderLinePerUnitWeightLabel = perUnit,
                SaleTotalWeight = saleTotalWeight,
            };
        }

        if (string.Equals(state.SetupType, "standard", StringComparison.OrdinalIgnoreCase))
            return AsUnits(new DisplayFieldsSnapshot(), cutting);

        var baseSnap = GetPortionStyleWeightSnapshots(state, quantity, variantIndex, variantWeightKg, product);

        if (string.Equals(state.SetupType, "by_weight", StringComparison.OrdinalIgnoreCase))
        {
            var gramsPerPortion = ToGramsFromConfigWeight(state.StartWeight, state.UnitRaw);
            if (gramsPerPortion <= 0)
                return AsUnits(baseSnap, cutting);
            var totalKg = gramsPerPortion * quantity / 1000m;
            var saleTotalWeight = FormatSaleTotalWeightFromTotalKg(totalKg);
            return new DisplayFieldsSnapshot
            {
                OrderLineQuantityMode = "weight",
                OrderLineCuttingLabel = cutting,
                UnitWeightGrams = gramsPerPortion,
                SaleUnits = FormatSaleUnitsHebrew(quantity),
                SaleTotalWeight = saleTotalWeight,
            };
        }

        if (string.Equals(state.SetupType, "by_unit_and_weight", StringComparison.OrdinalIgnoreCase))
        {
            var wCfg = state.UnitWeight > 0 ? state.UnitWeight : state.StartWeight;
            var gramsPerUnit = ToGramsFromConfigWeight(wCfg, state.UnitRaw);
            var perUnitLabel = gramsPerUnit > 0 ? FormatPerUnitWeightHebrew(gramsPerUnit, state.UnitRaw) : null;
            return AsUnits(baseSnap, cutting, perUnitLabel, null);
        }

        if (string.Equals(state.SetupType, "by_unit", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(state.UnitWeightMode, "variable", StringComparison.OrdinalIgnoreCase))
            {
                decimal grams = 0;
                if (variableWeightKg is > 0)
                    grams = ToGramsFromConfigWeight(variableWeightKg.Value, state.UnitRaw);
                var perUnitLabel = grams > 0 ? FormatPerUnitWeightHebrew(grams, state.UnitRaw) : null;
                return AsUnits(baseSnap, cutting, perUnitLabel, null);
            }

            if (string.Equals(state.UnitWeightMode, "by_variant", StringComparison.OrdinalIgnoreCase) || state.WeightByVariant)
            {
                var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
                var wKg = variantWeightKg is > 0 ? variantWeightKg!.Value : (v?.Weight ?? (decimal)state.UnitWeight);
                var gramsPerUnit = ToGramsFromConfigWeight(wKg, state.UnitRaw);
                var perUnitLabel = gramsPerUnit > 0 ? FormatPerUnitWeightHebrew(gramsPerUnit, state.UnitRaw) : null;
                var sizeLabel = FormatSizeWithApproxWeight(v, state.UnitRaw);
                return AsUnits(baseSnap, cutting, perUnitLabel, sizeLabel);
            }

            var gramsPu = ToGramsFromConfigWeight(state.UnitWeight, state.UnitRaw);
            var perUnit = gramsPu > 0 ? FormatPerUnitWeightHebrew(gramsPu, state.UnitRaw) : null;
            return AsUnits(baseSnap, cutting, perUnit, null);
        }

        return AsUnits(baseSnap, cutting);
    }

    private static DisplayFieldsSnapshot AsUnits(
        DisplayFieldsSnapshot baseSnap,
        string? cutting,
        string? perUnit = null,
        string? size = null) =>
        new()
        {
            OrderLineQuantityMode = "units",
            OrderLineCuttingLabel = cutting,
            OrderLinePerUnitWeightLabel = perUnit,
            OrderLineSizeLabel = size,
            UnitWeightGrams = baseSnap.UnitWeightGrams,
            SaleUnits = baseSnap.SaleUnits,
            SaleTotalWeight = null,
            ClearSaleTotalWeight = true,
        };

    /// <summary>Woo-style <c>unitWeightGrams</c> / <c>saleUnits</c> / per-portion <c>saleTotalWeight</c> for unit-mode lines (not total-kg lines).</summary>
    private static DisplayFieldsSnapshot GetPortionStyleWeightSnapshots(
        ProductPricingState state,
        decimal quantity,
        int variantIndex,
        decimal? variantWeightKg,
        Product product)
    {
        if (!state.IsWeighted) return new DisplayFieldsSnapshot();

        if (string.Equals(state.SetupType, "standard", StringComparison.OrdinalIgnoreCase))
            return new DisplayFieldsSnapshot();

        if (string.Equals(state.SetupType, "by_unit", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(state.UnitWeightMode, "variable", StringComparison.OrdinalIgnoreCase))
                return new DisplayFieldsSnapshot();

            if (string.Equals(state.UnitWeightMode, "by_variant", StringComparison.OrdinalIgnoreCase) || state.WeightByVariant)
            {
                var v = GetOrderedVariants(product).ElementAtOrDefault(variantIndex);
                var wKg = variantWeightKg is > 0 ? variantWeightKg.Value : (v?.Weight ?? (decimal)state.UnitWeight);
                var gramsPerUnit = ToGramsFromConfigWeight(wKg, state.UnitRaw);
                if (gramsPerUnit <= 0) return new DisplayFieldsSnapshot();
                return new DisplayFieldsSnapshot
                {
                    UnitWeightGrams = gramsPerUnit,
                    SaleUnits = FormatSaleUnitsHebrew(quantity),
                    SaleTotalWeight = FormatSaleTotalWeightPerPortion(gramsPerUnit),
                };
            }

            var grams = ToGramsFromConfigWeight(state.UnitWeight, state.UnitRaw);
            if (grams <= 0) return new DisplayFieldsSnapshot();
            return new DisplayFieldsSnapshot
            {
                UnitWeightGrams = grams,
                SaleUnits = FormatSaleUnitsHebrew(quantity),
                SaleTotalWeight = FormatSaleTotalWeightPerPortion(grams),
            };
        }

        if (string.Equals(state.SetupType, "by_unit_and_weight", StringComparison.OrdinalIgnoreCase))
        {
            var gramsPerUnit = ToGramsFromConfigWeight(state.UnitWeight, state.UnitRaw);
            if (gramsPerUnit <= 0) return new DisplayFieldsSnapshot();
            return new DisplayFieldsSnapshot
            {
                UnitWeightGrams = gramsPerUnit,
                SaleUnits = FormatSaleUnitsHebrew(quantity),
                SaleTotalWeight = FormatSaleTotalWeightPerPortion(gramsPerUnit),
            };
        }

        if (string.Equals(state.SetupType, "by_weight", StringComparison.OrdinalIgnoreCase))
        {
            var gramsPerUnit = ToGramsFromConfigWeight(state.StartWeight, state.UnitRaw);
            if (gramsPerUnit <= 0) return new DisplayFieldsSnapshot();
            return new DisplayFieldsSnapshot
            {
                UnitWeightGrams = gramsPerUnit,
                SaleUnits = FormatSaleUnitsHebrew(quantity),
                SaleTotalWeight = FormatSaleTotalWeightPerPortion(gramsPerUnit),
            };
        }

        return new DisplayFieldsSnapshot();
    }

    private sealed class DisplayFieldsSnapshot
    {
        public string? OrderLineQuantityMode { get; init; }
        public string? OrderLinePerUnitWeightLabel { get; init; }
        public string? OrderLineSizeLabel { get; init; }
        public string? OrderLineCuttingLabel { get; init; }
        public decimal? UnitWeightGrams { get; init; }
        public string? SaleUnits { get; init; }
        public string? SaleTotalWeight { get; init; }
        public bool ClearSaleTotalWeight { get; init; }
    }

    private readonly struct ProductPricingState
    {
        public string SetupType { get; init; }
        public bool IsWeighted { get; init; }
        public string UnitRaw { get; init; }
        public double UnitWeight { get; init; }
        public double StartWeight { get; init; }
        public string UnitWeightMode { get; init; }
        public bool WeightByVariant { get; init; }
    }

    private static ProductPricingState GetPricingState(Product product)
    {
        var setupType = product.SetupType?.Name ?? "standard";
        var wc = product.WeightConfig;
        var weightByVariant = wc?.WeightByVariant == true;
        var unitWeightMode = wc?.UnitWeightMode?.Name ?? (weightByVariant ? "by_variant" : "average");
        var isWeighted = product.IsWeighted == true && !string.Equals(setupType, "standard", StringComparison.OrdinalIgnoreCase);

        var unitRaw = string.IsNullOrWhiteSpace(wc?.Unit?.Name) ? "kg" : wc!.Unit!.Name.Trim().ToLowerInvariant();
        if (unitRaw != "g" && unitRaw != "kg")
            unitRaw = "kg";

        var unitWeight = ParseDouble(wc?.UnitWeight) ?? 0.5;
        var startWeight = ParseDouble(wc?.StartWeight) ?? (unitRaw == "g" ? 500 : 0.5);

        return new ProductPricingState
        {
            SetupType = setupType,
            IsWeighted = isWeighted,
            UnitRaw = unitRaw,
            UnitWeight = unitWeight,
            StartWeight = startWeight,
            UnitWeightMode = unitWeightMode,
            WeightByVariant = weightByVariant,
        };
    }

    private static double? ParseDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string GetUnitRaw(WeightConfig? wc)
    {
        var unitRaw = string.IsNullOrWhiteSpace(wc?.Unit?.Name) ? "kg" : wc!.Unit!.Name.Trim().ToLowerInvariant();
        return unitRaw != "g" && unitRaw != "kg" ? "kg" : unitRaw;
    }

    private static decimal ToGramsFromConfigWeight(double value, string unitRaw)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        return string.Equals(unitRaw, "g", StringComparison.OrdinalIgnoreCase)
            ? (decimal)Math.Round(value)
            : (decimal)Math.Round(value * 1000.0);
    }

    private static decimal ToGramsFromConfigWeight(decimal value, string unitRaw)
    {
        if (value <= 0) return 0;
        return string.Equals(unitRaw, "g", StringComparison.OrdinalIgnoreCase)
            ? decimal.Round(value, 0, MidpointRounding.AwayFromZero)
            : decimal.Round(value * 1000m, 0, MidpointRounding.AwayFromZero);
    }

    private static string FormatSaleUnitsHebrew(decimal q)
    {
        var r = decimal.Round(q, 3, MidpointRounding.AwayFromZero);
        var n = Math.Abs(r - decimal.Round(r, 0, MidpointRounding.AwayFromZero)) < 0.001m
            ? decimal.ToInt32(decimal.Round(r, 0, MidpointRounding.AwayFromZero))
            : decimal.Round(r, 2, MidpointRounding.AwayFromZero);
        return $"{n} יח'";
    }

    private static string FormatSaleTotalWeightPerPortion(decimal grams) => $"{decimal.ToInt32(decimal.Round(grams, 0, MidpointRounding.AwayFromZero))} גר'";

    private static string? FormatSaleTotalWeightFromTotalKg(decimal quantityKg)
    {
        var totalG = (int)Math.Round((double)(quantityKg * 1000m), MidpointRounding.AwayFromZero);
        if (totalG <= 0) return null;
        if (totalG >= 1000 && totalG % 1000 == 0) return $"{totalG / 1000} ק\"ג";
        if (totalG >= 1000)
        {
            var kg = totalG / 1000.0;
            var rounded = Math.Abs(kg - Math.Round(kg)) < 0.001 ? Math.Round(kg).ToString(CultureInfo.InvariantCulture) : Math.Round(kg, 1, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            return $"{rounded} ק\"ג";
        }
        return $"{totalG} גר'";
    }

    private static string FormatPerUnitWeightHebrew(decimal gramsPerUnit, string unitRaw)
    {
        if (gramsPerUnit <= 0) return "";
        if (string.Equals(unitRaw, "g", StringComparison.OrdinalIgnoreCase) || gramsPerUnit < 1000)
            return $"{decimal.ToInt32(decimal.Round(gramsPerUnit, 0, MidpointRounding.AwayFromZero))} גרם ליח'";
        var kg = gramsPerUnit / 1000m;
        var s = Math.Abs(kg - decimal.Round(kg, 0, MidpointRounding.AwayFromZero)) < 0.001m
            ? decimal.ToInt32(decimal.Round(kg, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
            : decimal.Round(kg, 2, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        return $"{s} ק\"ג ליח'";
    }

    private static string? FormatApproxWeightPhrase(decimal wKg, string unitRaw)
    {
        if (wKg <= 0) return null;
        if (string.Equals(unitRaw, "g", StringComparison.OrdinalIgnoreCase))
            return $"{decimal.ToInt32(decimal.Round(wKg * 1000m, 0, MidpointRounding.AwayFromZero))} גרם";
        if (wKg >= 1)
        {
            var s = Math.Abs(wKg - decimal.Round(wKg, 0, MidpointRounding.AwayFromZero)) < 0.001m
                ? decimal.ToInt32(decimal.Round(wKg, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
                : decimal.Round(wKg, 1, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            return $"{s} ק\"ג";
        }
        return $"{decimal.ToInt32(decimal.Round(wKg * 1000m, 0, MidpointRounding.AwayFromZero))} גרם";
    }

    private static string? FormatSizeWithApproxWeight(ProductVariant? v, string unitRaw)
    {
        if (v == null) return null;
        var ov = v.ProductVariantOptionValue?.ToDictionary(x => x.OptionName, x => x.OptionValue) ?? new Dictionary<string, string>();
        var sizeName = (ov.GetValueOrDefault("גודל") ?? ov.GetValueOrDefault("size") ?? "").Trim();
        var wKg = v.Weight ?? 0;
        var approx = FormatApproxWeightPhrase(wKg, unitRaw);
        if (string.IsNullOrEmpty(approx)) return string.IsNullOrEmpty(sizeName) ? null : sizeName;
        if (!string.IsNullOrEmpty(sizeName)) return $"{sizeName} (כ {approx})";
        var nonCutting = ov.FirstOrDefault(kv => !string.IsNullOrWhiteSpace(kv.Value) && !CuttingKeyHint.IsMatch(kv.Key));
        if (!string.IsNullOrWhiteSpace(nonCutting.Value)) return $"{nonCutting.Value} (כ {approx})";
        return $"(כ {approx})";
    }

    private static decimal? TryParseSaleUnitsCount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = LeadingNumberRegex.Match(s.Trim());
        if (!m.Success) return null;
        if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return null;
    }

    private static decimal? TryParseLeadingDecimalKg(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = LeadingNumberRegex.Match(s.Trim().Replace(',', '.'));
        if (!m.Success) return null;
        if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return null;
    }
}
