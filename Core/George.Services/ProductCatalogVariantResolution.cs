using System.Text;
using System.Text.RegularExpressions;
using George.DB;

namespace George.Services;

/// <summary>
/// Match order lines to catalog <see cref="ProductVariant"/> rows (Woo id, ProductVariantId, or option label).
/// Shared by quantity concentration, products report cut rows, and inventory-style stock classification.
/// </summary>
public static class ProductCatalogVariantResolution
{
    public static string AttrDedupeKey(string s)
    {
        var t = s.Trim().Normalize(NormalizationForm.FormKC);
        t = Regex.Replace(t, @"[\u201C\u201D\u201E\u0022״]", "\"");
        t = t.Replace('׳', '\'');
        t = Regex.Replace(t, @"\s+", " ");
        return t.ToLowerInvariant();
    }

    public static string FormatVariantDisplayLabel(ProductVariant v)
    {
        var opts = v.ProductVariantOptionValue?.OrderBy(o => o.OptionName).ToList()
                   ?? new List<ProductVariantOptionValue>();
        string raw;
        if (opts.Count == 0)
            raw = $"#{v.Id}";
        else if (opts.Count == 1)
            raw = opts[0].OptionValue.Trim();
        else
            raw = string.Join(" · ", opts.Select(o => $"{o.OptionName}: {o.OptionValue}"));
        return raw;
    }

    public static ProductVariant? FindVariantForOrderLine(Product p, OrderItem line)
    {
        if (line.ProductVariantId is int vid && vid > 0)
        {
            var byId = p.ProductVariant?.FirstOrDefault(v => !v.IsDeleted && v.Id == vid);
            if (byId != null) return byId;
            // Stale id (variants re-created since the order) — fall through to Woo id / label matching.
        }

        if (line.WooCommerceVariationId is int woo && woo > 0)
        {
            var byWooId = p.ProductVariant?.FirstOrDefault(v => !v.IsDeleted && v.WooCommerceVariationId == woo);
            if (byWooId != null) return byWooId;
        }

        var optionLabel = OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, p.Name);
        if (!string.IsNullOrWhiteSpace(optionLabel))
        {
            var key = AttrDedupeKey(optionLabel);
            foreach (var v in ProductCatalogStockClassification.ActiveVariants(p))
            {
                if (AttrDedupeKey(FormatVariantDisplayLabel(v)) == key)
                    return v;
                var opts = v.ProductVariantOptionValue?.Where(o => !string.IsNullOrWhiteSpace(o.OptionValue)).ToList();
                if (opts is { Count: > 0 }
                    && opts.Any(o => key.Contains(AttrDedupeKey(o.OptionValue), StringComparison.Ordinal)))
                    return v;
            }
        }

        // A VariantTitle like "750 גרם" is excluded from ResolveOptionDisplayLabel (reads as quantity
        // text), yet may be the exact name of a weight-named catalog option — exact-key match only.
        foreach (var candidate in new[] { line.VariantTitle, line.OrderLineCuttingLabel, line.OrderLineSizeLabel })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var rawKey = AttrDedupeKey(candidate);
            foreach (var v in ProductCatalogStockClassification.ActiveVariants(p))
            {
                if (AttrDedupeKey(FormatVariantDisplayLabel(v)) == rawKey)
                    return v;
            }
        }

        return null;
    }
}
