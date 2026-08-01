using System.Globalization;
using George.Common;
using George.DB;

namespace George.Services;

/// <summary>
/// Receipt-style discount layout for thermal vouchers — keep in sync with shop-manager
/// <c>voucherReceiptLayout.ts</c>.
/// </summary>
public static class VoucherReceiptLayout
{
    private const string ReceiptRowStyle =
        "display:flex;justify-content:space-between;align-items:baseline;gap:8px;font-size:12px;line-height:17px;";
    private const string ReceiptAmountStyle =
        "flex-shrink:0;white-space:nowrap;direction:ltr;unicode-bidi:embed;font-variant-numeric:tabular-nums;";

    public static string FormatAmount(decimal amount, bool negative = false)
    {
        var prefix = negative ? "−" : "";
        return $"{prefix}₪{Math.Abs(amount).ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    public sealed class LinePricing
    {
        public decimal Gross { get; init; }
        public decimal Discount { get; init; }
        public decimal Net { get; init; }
        public string DiscountLabel { get; init; } = "";
    }

    public static LinePricing? GetLinePricing(OrderItem item, decimal? lineGross)
    {
        var gross = lineGross is > 0m ? lineGross.Value : OrderedLineGross(item);
        if (gross <= 0m) return null;
        // DiscountAmount is kept paired with the line gross (picking save rescales it in OrderStorage).
        var discount = item.DiscountAmount is > 0m ? item.DiscountAmount.Value : 0m;
        if (discount <= 0m)
            return new LinePricing { Gross = gross, Discount = 0m, Net = gross, DiscountLabel = "" };
        var label = "הנחת מבצע";
        return new LinePricing
        {
            Gross = gross,
            Discount = discount,
            Net = Math.Max(0m, gross - discount),
            DiscountLabel = label,
        };
    }

    private static decimal OrderedLineGross(OrderItem item)
    {
        if (item.TotalPrice is > 0m) return item.TotalPrice.Value;
        var q = item.Quantity;
        var pu = item.PricePerUnit ?? 0m;
        if (q > 0m && pu > 0m) return q * pu;
        return 0m;
    }

    public sealed class Summary
    {
        public decimal MerchandiseGross { get; init; }
        public decimal PromotionDiscount { get; init; }
        public decimal ManualDiscount { get; init; }
        public decimal Shipping { get; init; }
        public decimal GrandTotal { get; init; }
        public decimal TotalSaved { get; init; }
        public string? CouponCode { get; init; }
        public string ManualDiscountLabel { get; init; } = "הנחה ידנית";
        public bool ShowSubtotal { get; init; }
    }

    public static Summary BuildSummary(Order order, decimal merchandiseGross)
    {
        var items = order.OrderItem ?? new List<OrderItem>();
        // Same rule as OrderService.SumStampedPromotionDiscount: once picking started, only
        // meaningfully-picked lines contribute their discount — the discount follows its merchandise
        // (DiscountAmount is kept paired with the line gross by the picking save).
        var activeItems = items.Where(i => !i.IsDeleted).ToList();
        var anyPicked = activeItems.Any(OrderItemLineDisplay.OrderMeaningfulPick);
        var promo = activeItems.Sum(i =>
        {
            if (i.DiscountAmount is not > 0m) return 0m;
            if (!anyPicked) return i.DiscountAmount.Value;
            return OrderItemLineDisplay.OrderMeaningfulPick(i) && i.PickedQuantity is > 0m
                ? i.DiscountAmount.Value
                : 0m;
        });
        var manual = order.ManualDiscountAmount is > 0m ? order.ManualDiscountAmount.Value : 0m;
        var shipping = order.ShippingCost ?? 0m;
        var grand = OrderDiscountTotals.ComputeGrandTotal(merchandiseGross, shipping, promo, manual);
        var coupon = string.IsNullOrWhiteSpace(order.CouponCode) ? null : order.CouponCode.Trim();
        var manualLabel = "הנחה ידנית";
        if (string.Equals(order.ManualDiscountType, "percent", StringComparison.OrdinalIgnoreCase)
            && order.ManualDiscountValue is > 0m)
        {
            manualLabel = $"הנחה ({order.ManualDiscountValue.Value.ToString("0.##", CultureInfo.InvariantCulture)}%)";
        }

        var showSubtotal = merchandiseGross > 0m
            && (promo > 0m || manual > 0m || shipping > 0m || !string.IsNullOrEmpty(coupon));

        return new Summary
        {
            MerchandiseGross = merchandiseGross,
            PromotionDiscount = promo,
            ManualDiscount = manual,
            Shipping = shipping,
            GrandTotal = grand,
            TotalSaved = promo + manual,
            CouponCode = coupon,
            ManualDiscountLabel = manualLabel,
            ShowSubtotal = showSubtotal,
        };
    }

    public static string SummaryHtml(Summary summary, string payLine, string vatLabel, Func<string, string> escapeHtml)
    {
        var rows = new List<string>();
        if (summary.ShowSubtotal)
            rows.Add(ReceiptRowHtml("סכום ביניים", escapeHtml(FormatAmount(summary.MerchandiseGross))));
        if (summary.PromotionDiscount > 0m)
            rows.Add(ReceiptRowHtml("הנחות מבצע", escapeHtml(FormatAmount(summary.PromotionDiscount, negative: true))));
        if (summary.ManualDiscount > 0m)
            rows.Add(ReceiptRowHtml(escapeHtml(summary.ManualDiscountLabel), escapeHtml(FormatAmount(summary.ManualDiscount, negative: true))));
        if (!string.IsNullOrEmpty(summary.CouponCode))
            rows.Add(ReceiptRowHtml($"קופון ({escapeHtml(summary.CouponCode.ToUpperInvariant())})", "—", "font-size:11px;color:#374151;"));
        if (summary.Shipping > 0m)
            rows.Add(ReceiptRowHtml("משלוח", escapeHtml(FormatAmount(summary.Shipping))));

        var totalRow =
            $"<div style=\"display:flex;justify-content:space-between;align-items:baseline;gap:8px;margin-top:6px;padding-top:6px;border-top:1px solid #000;font-size:14px;font-weight:700;line-height:18px;\">" +
            $"<span>סה״כ לתשלום</span><span style=\"{ReceiptAmountStyle}\">{escapeHtml(FormatAmount(summary.GrandTotal))}</span></div>";
        var saved = summary.TotalSaved > 0m
            ? $"<div style=\"margin-top:6px;text-align:center;font-size:11px;font-weight:600;line-height:15px;\">חסכת {escapeHtml(FormatAmount(summary.TotalSaved))}</div>"
            : "";

        return
            $"<div style=\"margin-bottom:12px;padding:10px 0 12px;border-bottom:1px dashed #000;\" dir=\"rtl\">" +
            string.Join("", rows) +
            totalRow +
            $"<div style=\"margin-top:8px;text-align:center;font-size:13px;font-weight:600;line-height:17px;\">{escapeHtml(payLine)}</div>" +
            $"<div style=\"margin-top:4px;text-align:center;font-size:24px;font-weight:800;line-height:28px;direction:ltr;unicode-bidi:embed;font-variant-numeric:tabular-nums;\">{escapeHtml(FormatAmount(summary.GrandTotal))}</div>" +
            $"<div style=\"margin-top:4px;text-align:center;font-size:11px;font-weight:400;line-height:15px;color:#374151;\">{escapeHtml(vatLabel)}</div>" +
            saved +
            "</div>";
    }

    public static string PickedTotalCellHtml(LinePricing? pricing, string fallbackTotal, string valStyle, Func<string, string> escapeHtml)
    {
        if (pricing is null || pricing.Discount <= 0m || fallbackTotal == "—")
            return $"<div style=\"{valStyle}\"><bdi dir=\"ltr\">{escapeHtml(fallbackTotal)}</bdi></div>";
        return
            $"<div style=\"{valStyle}line-height:12px;\">" +
            $"<div style=\"font-size:9px;font-weight:400;text-decoration:line-through;color:#6b7280;\"><bdi dir=\"ltr\">{escapeHtml(FormatAmount(pricing.Gross))}</bdi></div>" +
            $"<div><bdi dir=\"ltr\">{escapeHtml(FormatAmount(pricing.Net))}</bdi></div>" +
            "</div>";
    }

    private static string ReceiptRowHtml(string label, string amount, string? extraStyle = null)
    {
        var style = string.IsNullOrEmpty(extraStyle) ? ReceiptRowStyle : $"{ReceiptRowStyle}{extraStyle}";
        return $"<div style=\"{style}\"><span style=\"text-align:right;min-width:0;\">{label}</span><span style=\"{ReceiptAmountStyle}\">{amount}</span></div>";
    }
}
