using George.DB;

namespace George.Services.Payments.Cardcom;

public static class CardcomDocumentBuilder
{
    public const string DefaultDocumentType = "TaxInvoiceAndReceipt";

    public static CardcomTransactionDocument Build(
        Order order,
        IEnumerable<OrderItem> items,
        string documentTypeToCreate = DefaultDocumentType,
        bool sendByEmail = false,
        bool sendBySms = false,
        int isoCoinId = 1)
    {
        var lines = BuildProductLines(order, items).ToList();
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

        var comments = $"הזמנה {order.OrderNumber}";
        if (!string.IsNullOrWhiteSpace(order.CustomerNote))
        {
            var note = order.CustomerNote.Trim();
            if (note.Length > 200)
                note = note[..200];
            comments = $"{comments} — {note}";
        }

        return new CardcomTransactionDocument
        {
            DocumentTypeToCreate = string.IsNullOrWhiteSpace(documentTypeToCreate)
                ? DefaultDocumentType
                : documentTypeToCreate.Trim(),
            Name = string.IsNullOrWhiteSpace(order.CustomerName) ? "לקוח" : order.CustomerName.Trim(),
            Email = order.CustomerEmail?.Trim(),
            Phone = order.CustomerPhone?.Trim(),
            SendByEmail = sendByEmail && !string.IsNullOrWhiteSpace(order.CustomerEmail),
            SendBySms = sendBySms && !string.IsNullOrWhiteSpace(order.CustomerPhone),
            Comments = comments.Length > 250 ? comments[..250] : comments,
            ExternalId = order.Id.ToString(),
            Language = "he",
            IsoCoinId = isoCoinId,
            Products = lines,
        };
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
