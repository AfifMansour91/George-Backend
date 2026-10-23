using George.Services;
using George.DB;

namespace George.Services.Payments.Cardcom;

public static class CardcomDocumentBuilder
{
    public const string DefaultDocumentType = "TaxInvoiceAndReceipt";
    public const string RefundDocumentType = "TaxInvoiceAndReceiptRefund";

    /// <summary>Catalog price on every line + one "הנחה" ADJUSTMENT line before the total (default).</summary>
    public const string PromotionDisplayDiscountTotal = "discount_total";
    /// <summary>Deal ₪/unit in the product line with the promotion name; no discount line (InvoicePromotionAsLinePrice = 1).</summary>
    public const string PromotionDisplayNetPrice = "net_price";
    /// <summary>
    /// Catalog price on the product line + a negative "הנחה - {promotion} ({product})" line right under it. Cardcom's
    /// own recommendation (2026-10-14) was a per-line discount; its v11 Products schema only has ProductID /
    /// Description / Quantity / UnitCost / TotalLineCost / IsVatFree / IsGiftCard, so the discount is its own line
    /// next to the product. Coupon / manual discounts still reconcile through ADJUSTMENT.
    /// </summary>
    public const string PromotionDisplayLineDiscount = "line_discount";
    /// <summary>Cardcom rejects negative TotalLineCost on documents; keep every negative line as quantity 1.</summary>
    public const string DiscountLineProductId = "DISCOUNT";

    /// <summary>Site mode column first; legacy bool second (true = net_price); default discount_total.</summary>
    public static string ResolvePromotionDisplayMode(string? mode, bool? legacyAsLinePrice)
    {
        var m = (mode ?? "").Trim().ToLowerInvariant();
        if (m is PromotionDisplayDiscountTotal or PromotionDisplayNetPrice or PromotionDisplayLineDiscount)
            return m;
        return legacyAsLinePrice == true ? PromotionDisplayNetPrice : PromotionDisplayDiscountTotal;
    }

    public static CardcomTransactionDocument Build(
        Order order,
        IEnumerable<OrderItem> items,
        string documentTypeToCreate = DefaultDocumentType,
        bool sendByEmail = false,
        bool sendBySms = false,
        int isoCoinId = 1,
        decimal? amountOverride = null,
        bool promotionAsLinePrice = false,
        string? promotionDisplayMode = null)
    {
        var displayMode = ResolvePromotionDisplayMode(promotionDisplayMode, promotionAsLinePrice);
        List<CardcomDocumentProductLine> lines;
        if (amountOverride is > 0)
        {
            // Partial-refund credit note: a single line for the refunded amount. The itemized
            // list sums to the full order total, and Cardcom rejects a document whose total
            // differs from the linked (partial) refund transaction.
            lines = new List<CardcomDocumentProductLine>
            {
                new()
                {
                    ProductId = order.OrderNumber,
                    Description = $"זיכוי חלקי - הזמנה {order.OrderNumber}",
                    Quantity = 1,
                    UnitCost = amountOverride.Value,
                },
            };
        }
        else
        {
            lines = BuildProductLines(order, items, displayMode).ToList();
            if (lines.Count == 0)
            {
                var amount = order.Total ?? order.SubTotal ?? 0m;
                if (amount > 0)
                {
                    lines.Add(new CardcomDocumentProductLine
                    {
                        ProductId = order.OrderNumber,
                        Description = $"הזמנה {order.OrderNumber}",
                        Quantity = 1,
                        UnitCost = amount,
                    });
                }
            }

            // Coupon / manual / promotion discounts are not item lines, so Σ lines can exceed the
            // actually-charged Order.Total - Cardcom then rejects the document ("Total items not
            // equal to some form of payment"). Reconcile with an explicit adjustment line so the
            // document always equals the captured payment.
            var orderTotal = order.Total;
            if (orderTotal is > 0 && lines.Count > 0)
            {
                var documentTotal = lines.Sum(l =>
                    l.TotalLineCost is > 0 ? l.TotalLineCost.Value : Math.Round(l.Quantity * l.UnitCost, 2, MidpointRounding.AwayFromZero));
                var diff = Math.Round(orderTotal.Value - documentTotal, 2, MidpointRounding.AwayFromZero);
                if (diff != 0)
                {
                    lines.Add(new CardcomDocumentProductLine
                    {
                        ProductId = "ADJUSTMENT",
                        Description = diff < 0 ? "הנחה" : "התאמת סכום",
                        Quantity = 1,
                        UnitCost = diff,
                    });
                }
            }
        }

        var comments = $"הזמנה {order.OrderNumber}";
        if (!string.IsNullOrWhiteSpace(order.CustomerNote))
        {
            var note = order.CustomerNote.Trim();
            if (note.Length > 200)
                note = note[..200];
            comments = $"{comments} - {note}";
        }

        var (addressLine1, addressLine2, addressCity) = BuildDeliveryAddress(order);
        var (recipientName, recipientTaxId) = InvoiceRecipient.Resolve(order);

        return new CardcomTransactionDocument
        {
            DocumentTypeToCreate = string.IsNullOrWhiteSpace(documentTypeToCreate)
                ? DefaultDocumentType
                : documentTypeToCreate.Trim(),
            Name = string.IsNullOrWhiteSpace(recipientName) ? "לקוח" : recipientName,
            TaxId = recipientTaxId,
            Email = order.CustomerEmail?.Trim(),
            Phone = order.CustomerPhone?.Trim(),
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = addressCity,
            SendByEmail = sendByEmail && !string.IsNullOrWhiteSpace(order.CustomerEmail),
            SendBySms = sendBySms && !string.IsNullOrWhiteSpace(order.CustomerPhone),
            Comments = comments.Length > 250 ? comments[..250] : comments,
            ExternalId = order.Id.ToString(),
            Language = "he",
            IsoCoinId = isoCoinId,
            Products = lines,
        };
    }

    /// <summary>
    /// Delivery-order invoice address. AddressLine2 carries the extras with explicit Hebrew
    /// labels (קומה/דירה/קוד כניסה) so they read clearly on the document, not as bare numbers.
    /// Cardcom caps each address field at 50 chars. Pickup orders get no address.
    /// </summary>
    private static (string? Line1, string? Line2, string? City) BuildDeliveryAddress(Order order)
    {
        if (string.Equals(order.DeliveryType?.Trim(), "Pickup", StringComparison.OrdinalIgnoreCase))
            return (null, null, null);

        var street = Clip(order.DeliveryStreet);
        var city = Clip(order.DeliveryCity);
        // Older orders may only have the composed single-line address.
        if (street is null && city is null)
            street = Clip(order.DeliveryAddress);

        var extras = new[]
            {
                string.IsNullOrWhiteSpace(order.DeliveryFloor) ? null : $"קומה {order.DeliveryFloor.Trim()}",
                string.IsNullOrWhiteSpace(order.DeliveryApartment) ? null : $"דירה {order.DeliveryApartment.Trim()}",
                string.IsNullOrWhiteSpace(order.DeliveryEntranceCode) ? null : $"קוד כניסה {order.DeliveryEntranceCode.Trim()}",
            }
            .Where(s => s != null)
            .ToList();
        var line2 = extras.Count > 0 ? Clip(string.Join(", ", extras)) : null;

        return (street, line2, city);
    }

    private static string? Clip(string? value, int max = 50)
    {
        var s = value?.Trim();
        if (string.IsNullOrEmpty(s))
            return null;
        return s.Length > max ? s[..max] : s;
    }

    public static int MapCurrencyToIsoCoinId(string? currency) =>
        (currency ?? "ILS").Trim().ToUpperInvariant() switch
        {
            "USD" => 2,
            _ => 1,
        };

    /// <param name="promotionAsLinePrice">
    /// Site.InvoicePromotionAsLinePrice: a line with a stamped discount is printed at its NET price per unit
    /// (net ÷ quantity, TotalLineCost = net) with the promotion name appended, e.g. "בשר טחון - 2 ק"ג ב-120 ₪",
    /// 2.17 × 60.00 = 130.20 - so the document needs no "הנחה" adjustment line for promotions. Off (default) keeps
    /// the catalog price per unit on every line and reconciles through the ADJUSTMENT line.
    /// </param>
    private static IEnumerable<CardcomDocumentProductLine> BuildProductLines(Order order, IEnumerable<OrderItem> items, string displayMode = PromotionDisplayDiscountTotal)
    {
        var promotionAsLinePrice = displayMode == PromotionDisplayNetPrice;
        var promotionAsLineDiscount = displayMode == PromotionDisplayLineDiscount;
        // Bundle children are informational (the parent line carries the bundle money) - never invoice them.
        foreach (var item in BundleOrderLines.WithoutChildren(items.Where(i => !i.IsDeleted)).OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
        {
            var qty = item.PickedQuantity ?? item.Quantity;
            if (qty <= 0)
                continue;

            var lineTotal = item.TotalPrice;
            var unitCost = item.PricePerUnit;
            if (unitCost is null or <= 0 && lineTotal is > 0 && qty > 0)
                unitCost = Math.Round(lineTotal.Value / qty, 2, MidpointRounding.AwayFromZero);
            if (unitCost is null or <= 0)
                continue;

            // פחת is MORE weight billed at the catalog rate, never a higher rate: a line picked with פחת
            // is invoiced as the gross weight (charged total ÷ catalog ₪/kg) × the catalog price, so the
            // customer reads 4.0125 kg × ₪120, not 3.21 kg × ₪150 (Dagei Gat order 7710). The net weight
            // that was actually weighed is spelled out in the description.
            var depreciationNet = (decimal?)null;
            if (item.DepreciationPercent is > 0m && item.PricePerUnit is > 0m && lineTotal is > 0m)
            {
                depreciationNet = qty;
                qty = Math.Round(lineTotal.Value / item.PricePerUnit.Value, 4, MidpointRounding.AwayFromZero);
                unitCost = item.PricePerUnit.Value;
            }
            // Weight items: PricePerUnit is the sale-unit price, not per picked-quantity unit, so
            // qty×unit can be far off the actually-charged TotalPrice (e.g. picked 0.5 × ₪77.50
            // while the line charged ₪77.50). Cardcom validates document total = Σ qty×unit against
            // the linked transaction (TotalLineCost only absorbs rounding), so re-derive the unit
            // price from the charged line total whenever they disagree beyond rounding. (A פחת line
            // differs only by the 4th-decimal rounding of its gross weight - TotalLineCost absorbs it.)
            else if (lineTotal is > 0 && Math.Abs(Math.Round(unitCost.Value * qty, 2, MidpointRounding.AwayFromZero) - lineTotal.Value) >= 0.01m)
                unitCost = Math.Round(lineTotal.Value / qty, 2, MidpointRounding.AwayFromZero);

            var description = OrderItemLineDisplay.FormatDocumentLineDescription(item);
            if (item.DepreciationPercent is > 0m)
                description += depreciationNet.HasValue
                    ? $" (כולל פחת {item.DepreciationPercent.Value:0.##}%, משקל נטו {depreciationNet.Value:0.###} ק\"ג)"
                    : $" (כולל פחת {item.DepreciationPercent.Value:0.##}%)";

            // Promotion line as net price: the customer reads the deal's ₪/unit, not catalog price + a
            // document-level discount. Net = TotalPrice − DiscountAmount (George's line net everywhere).
            if (promotionAsLinePrice && item.DiscountAmount is > 0m && lineTotal is > 0m && qty > 0m)
            {
                var net = Math.Max(0m, Math.Round(lineTotal.Value - item.DiscountAmount.Value, 2, MidpointRounding.AwayFromZero));
                lineTotal = net;
                unitCost = Math.Round(net / qty, 2, MidpointRounding.AwayFromZero);
                var promotionName = item.Promotion?.Name?.Trim();
                description += string.IsNullOrEmpty(promotionName) ? " - מבצע" : $" - {promotionName}";
            }

            if (description.Length > 250)
                description = description[..250];

            yield return new CardcomDocumentProductLine
            {
                ProductId = item.LineSku ?? item.ProductId?.ToString(),
                Description = description,
                Quantity = qty,
                UnitCost = unitCost.Value,
                TotalLineCost = lineTotal,
            };

            // Line-discount mode: the product keeps its catalog price above, and its discount is the very next
            // line, named after the promotion and the product, so the customer sees which product got the deal
            // (Cardcom support 2026-10-14; הקצב בעיר). Net of both lines = TotalPrice − DiscountAmount, so no
            // document-level "הנחה" is needed for promotions.
            if (promotionAsLineDiscount && item.DiscountAmount is > 0m && lineTotal is > 0m)
            {
                var discount = Math.Round(Math.Min(item.DiscountAmount.Value, lineTotal.Value), 2, MidpointRounding.AwayFromZero);
                if (discount > 0m)
                {
                    var promotionName = item.Promotion?.Name?.Trim();
                    var productName = (item.Title ?? "").Trim();
                    var discountDescription = string.IsNullOrEmpty(promotionName) ? "הנחה - מבצע" : $"הנחה - {promotionName}";
                    if (!string.IsNullOrEmpty(productName))
                        discountDescription += $" ({productName})";
                    if (discountDescription.Length > 250)
                        discountDescription = discountDescription[..250];
                    yield return new CardcomDocumentProductLine
                    {
                        ProductId = DiscountLineProductId,
                        Description = discountDescription,
                        Quantity = 1,
                        UnitCost = -discount,
                    };
                }
            }
        }

        var shipping = order.ShippingCost ?? 0m;
        if (shipping > 0)
        {
            yield return new CardcomDocumentProductLine
            {
                ProductId = "SHIPPING",
                Description = "משלוח",
                Quantity = 1,
                UnitCost = shipping,
            };
        }
    }
}
