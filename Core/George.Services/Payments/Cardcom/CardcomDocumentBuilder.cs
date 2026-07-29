using George.DB;

namespace George.Services.Payments.Cardcom;

public static class CardcomDocumentBuilder
{
    public const string DefaultDocumentType = "TaxInvoiceAndReceipt";
    public const string RefundDocumentType = "TaxInvoiceAndReceiptRefund";

    public static CardcomTransactionDocument Build(
        Order order,
        IEnumerable<OrderItem> items,
        string documentTypeToCreate = DefaultDocumentType,
        bool sendByEmail = false,
        bool sendBySms = false,
        int isoCoinId = 1,
        decimal? amountOverride = null)
    {
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
                    Description = $"זיכוי חלקי — הזמנה {order.OrderNumber}",
                    Quantity = 1,
                    UnitCost = amountOverride.Value,
                },
            };
        }
        else
        {
            lines = BuildProductLines(order, items).ToList();
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
            // actually-charged Order.Total — Cardcom then rejects the document ("Total items not
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
            comments = $"{comments} — {note}";
        }

        var (addressLine1, addressLine2, addressCity) = BuildDeliveryAddress(order);

        return new CardcomTransactionDocument
        {
            DocumentTypeToCreate = string.IsNullOrWhiteSpace(documentTypeToCreate)
                ? DefaultDocumentType
                : documentTypeToCreate.Trim(),
            Name = string.IsNullOrWhiteSpace(order.CustomerName) ? "לקוח" : order.CustomerName.Trim(),
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

    private static IEnumerable<CardcomDocumentProductLine> BuildProductLines(Order order, IEnumerable<OrderItem> items)
    {
        foreach (var item in items.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
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

            // Weight items: PricePerUnit is the sale-unit price, not per picked-quantity unit, so
            // qty×unit can be far off the actually-charged TotalPrice (e.g. picked 0.5 × ₪77.50
            // while the line charged ₪77.50). Cardcom validates document total = Σ qty×unit against
            // the linked transaction (TotalLineCost only absorbs rounding), so re-derive the unit
            // price from the charged line total whenever they disagree beyond rounding.
            if (lineTotal is > 0 && Math.Abs(Math.Round(unitCost.Value * qty, 2, MidpointRounding.AwayFromZero) - lineTotal.Value) >= 0.01m)
                unitCost = Math.Round(lineTotal.Value / qty, 2, MidpointRounding.AwayFromZero);

            var description = string.Join(" — ", new[]
                {
                    item.Title,
                    item.VariantTitle,
                }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            if (string.IsNullOrWhiteSpace(description))
                description = "פריט";

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
