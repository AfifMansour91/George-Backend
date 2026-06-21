using System;
using System.Collections.Generic;
using George.DB;

namespace George.Services;

/// <summary>
/// WooCommerce product "תוויות חנות" ↔ ACF true/false fields on the product.
/// Field reference IDs must match the ACF field keys in WordPress (post meta <c>_fieldname</c>).
/// Update <see cref="WooAcfStoreLabelFieldRefs"/> if the customer's ACF is re-exported with new keys.
/// </summary>
public partial class WooCommerceService
{
    /// <summary>Maps logical meta key (matches ACF <c>data-name</c> / REST meta key) → ACF field_* reference.</summary>
    private static readonly IReadOnlyDictionary<string, string> WooAcfStoreLabelFieldRefs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["new"] = "field_69f5c7cfeadce",
            ["kosher_for_passover"] = "field_69f31501afac2",
            ["bestseller"] = "field_69fd9d56e6b78",
            ["frozen"] = "field_69f314dfafac1",
            ["readytocook"] = "field_69fd9e87e6b79",
            ["natural"] = "field_69fd9b15b98d2",
            ["sugarfree"] = "field_69fd9b4fb98d3",
            ["gluten_free"] = "field_69f314a8afac0",
            ["lactosefree"] = "field_69fd9d33e6b77",
            ["not_kosher"] = "field_69f3154eafac3",
        };

    /// <summary>
    /// Boolean storefront labels for <c>POST /wp-json/ed/v1/product-label</c> (<c>labels</c> object).
    /// Keys must match existing ACF field names on the WooCommerce site.
    /// </summary>
    public static Dictionary<string, bool> BuildEdV1ProductLabels(Product product)
    {
        var now = DateTime.UtcNow;
        var passoverEffective = product.LabelKosherForPassover &&
                                (!product.LabelKosherForPassoverEndDate.HasValue ||
                                 product.LabelKosherForPassoverEndDate.Value > now);
        var newEffective = product.LabelNew &&
                           (!product.LabelNewEndDate.HasValue || product.LabelNewEndDate.Value > now);

        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["frozen"] = product.LabelFrozen,
            ["gluten_free"] = product.LabelGlutenFree,
            ["not_kosher"] = product.LabelNotKosher,
            ["kosher_for_passover"] = passoverEffective,
            ["bestseller"] = product.LabelBestseller,
            ["low_availability"] = product.LabelLowAvailability,
            ["readytocook"] = product.LabelReadyToCook,
            ["natural"] = product.LabelNatural,
            ["sugarfree"] = product.LabelSugarFree,
            ["lactosefree"] = product.LabelLactoseFree,
            ["new"] = newEffective,
        };
    }

    /// <summary>
    /// Appends ACF true/false meta pairs (<c>key</c> + <c>_{key}</c> field reference) so WooCommerce REST updates match wp-admin.
    /// </summary>
    private static void AppendWooAcfStoreLabelMeta(ICollection<object> metaData, Product product)
    {
        var labels = BuildEdV1ProductLabels(product);

        void AddTrueFalse(string logicalMetaKey, string fieldRef, bool on)
        {
            metaData.Add(new { key = logicalMetaKey, value = on ? "1" : "0" });
            metaData.Add(new { key = "_" + logicalMetaKey, value = fieldRef });
        }

        AddTrueFalse("new", WooAcfStoreLabelFieldRefs["new"], labels["new"]);
        AddTrueFalse("kosher_for_passover", WooAcfStoreLabelFieldRefs["kosher_for_passover"], labels["kosher_for_passover"]);
        AddTrueFalse("bestseller", WooAcfStoreLabelFieldRefs["bestseller"], labels["bestseller"]);
        AddTrueFalse("frozen", WooAcfStoreLabelFieldRefs["frozen"], labels["frozen"]);
        AddTrueFalse("readytocook", WooAcfStoreLabelFieldRefs["readytocook"], labels["readytocook"]);
        AddTrueFalse("natural", WooAcfStoreLabelFieldRefs["natural"], labels["natural"]);
        AddTrueFalse("sugarfree", WooAcfStoreLabelFieldRefs["sugarfree"], labels["sugarfree"]);
        AddTrueFalse("gluten_free", WooAcfStoreLabelFieldRefs["gluten_free"], labels["gluten_free"]);
        AddTrueFalse("lactosefree", WooAcfStoreLabelFieldRefs["lactosefree"], labels["lactosefree"]);
        AddTrueFalse("not_kosher", WooAcfStoreLabelFieldRefs["not_kosher"], labels["not_kosher"]);

        // Optional end dates (if the site adds ACF date fields with these keys)
        if (product.LabelKosherForPassoverEndDate.HasValue)
        {
            var iso = product.LabelKosherForPassoverEndDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            metaData.Add(new { key = "kosher_for_passover_end_date", value = iso });
        }

        if (product.LabelNewEndDate.HasValue)
        {
            var iso = product.LabelNewEndDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            metaData.Add(new { key = "label_new_end_date", value = iso });
            metaData.Add(new { key = "new_end_date", value = iso });
        }
    }

    /// <summary>
    /// Reads an ACF true/false from Woo <c>meta_data</c>: tries logical keys and aliases, then the raw <c>field_*</c> id.
    /// Skips values that look like ACF field references (<c>field_…</c>) so <c>_frozen</c>-style rows don't mask the real 0/1.
    /// </summary>
    private static bool WooImportMetaBoolTrueFalse(
        IReadOnlyList<WooImportMetaEntry>? meta,
        string logicalKey,
        params string[] extraKeys)
    {
        var keys = new List<string> { logicalKey };
        keys.AddRange(extraKeys);
        if (WooAcfStoreLabelFieldRefs.TryGetValue(logicalKey, out var fieldRef) && !string.IsNullOrEmpty(fieldRef))
            keys.Add(fieldRef);

        foreach (var k in keys)
        {
            var v = MetaString(meta, k);
            if (string.IsNullOrWhiteSpace(v)) continue;
            var t = v.Trim();
            if (t.StartsWith("field_", StringComparison.OrdinalIgnoreCase))
                continue;
            return IsMetaYes(t);
        }

        return false;
    }
}
