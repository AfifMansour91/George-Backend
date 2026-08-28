using System.Text.RegularExpressions;
using George.DB;

namespace George.Services;

/// <summary>
/// Option / variant labels for reports - use <see cref="OrderItem.VariantTitle"/> as stored on the line (Woo/manual),
/// without merging computed <see cref="OrderItem.OrderLineSizeLabel"/> (approx weight in גרם duplicates names).
/// </summary>
public static class OrderItemReportLineLabel
{
    /// <summary>
    /// Single display label for a variant/cut row: VariantTitle, else cutting, else size (no concatenation).
    /// </summary> 
    public static string? ResolveOptionDisplayLabel(OrderItem line, string? productName = null)
    {
        var pn = (productName ?? "").Trim();
        var vt = (line.VariantTitle ?? "").Trim();
        if (vt.Length > 0 && !IsNonOptionDisplayLabel(vt)
            && (pn.Length == 0 || !string.Equals(vt, pn, StringComparison.OrdinalIgnoreCase)))
            return vt;

        var cut = (line.OrderLineCuttingLabel ?? "").Trim();
        if (cut.Length > 0 && !IsNonOptionDisplayLabel(cut)) return cut;

        var size = (line.OrderLineSizeLabel ?? "").Trim();
        if (size.Length > 0 && !IsNonOptionDisplayLabel(size)) return size;

        return null;
    }

    /// <summary>
    /// Quantity / per-unit weight text from order snapshots - not a catalog option name (no detail row in reports).
    /// </summary>
    public static bool IsNonOptionDisplayLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return true;
        var t = label.Trim();
        if (IsGenericVariantTitle(t)) return true;
        if (Regex.IsMatch(t, @"^[\d.,]+\s*יח", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^[\d.,]+\s*גרם(\s*ליח')?", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^[\d.,]+\s*גר'", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^[\d.,]+\s*ק[״""\u0022']?\s*ג(\s*ליח')?", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^\(כ\s*[\d.,]+", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    /// <summary>Line label for report detail rows (quantity concentration, etc.).</summary>
    public static string BuildLineLabel(OrderItem line, string? productName = null)
    {
        var label = ResolveOptionDisplayLabel(line, productName);
        if (!string.IsNullOrEmpty(label)) return label;

        var sul = (line.SaleUnitsLine ?? "").Trim();
        if (sul.Length > 0) return sul;

        var title = (line.Title ?? "").Trim();
        return string.IsNullOrEmpty(title) ? "-" : title;
    }

    public static bool IsGenericVariantTitle(string vt)
    {
        var s = vt.Trim();
        if (s.Length == 0) return true;
        if (s == "יחידה") return true;
        if (string.Equals(s, "kg", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(s, @"^ק[״""\u0022']?\s*ג$")) return true;
        return false;
    }
}
