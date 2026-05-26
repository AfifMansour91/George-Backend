using System.Globalization;
using George.DB;

namespace George.Services;

/// <summary>Placeholder replacement for order and payment SMS templates.</summary>
public static class NotificationMessageHelper
{
    public static string ReplaceOrderPlaceholders(string template, Order order, decimal? orderTotalOverride = null)
    {
        var orderDate = order.CreationTime;
        var deliveryDate = order.DeliveryDate;
        var pickupDate = order.PickupDate;
        var orderTotal = orderTotalOverride ?? ResolveOrderTotalForPlaceholders(order);
        var orderTotalStr = orderTotal.ToString("N2", CultureInfo.InvariantCulture);
        return template
            .Replace("[customer_name]", order.CustomerName ?? "")
            .Replace("[order_number]", order.OrderNumber ?? "")
            .Replace("[order_date]", orderDate.ToString("dd/MM/yyyy"))
            .Replace("[order_total]", orderTotalStr)
            .Replace("[delivery_date]", deliveryDate.HasValue ? deliveryDate.Value.ToString("dd/MM/yyyy") : "")
            .Replace("[delivery_time]", order.DeliveryTime ?? "")
            .Replace("[pickup_date]", pickupDate.HasValue ? pickupDate.Value.ToString("dd/MM/yyyy") : "")
            .Replace("[pickup_time]", order.PickupTime ?? "");
    }

    public static string ReplacePaymentPlaceholders(
        string template,
        Order order,
        string? storeName = null,
        string? invoiceNumber = null,
        string? documentUrl = null,
        string? paymentUrl = null,
        decimal? refundAmount = null)
    {
        var body = ReplaceOrderPlaceholders(template, order);
        var refundStr = refundAmount.HasValue
            ? refundAmount.Value.ToString("N2", CultureInfo.InvariantCulture)
            : (order.Total?.ToString("N2", CultureInfo.InvariantCulture) ?? "");
        return body
            .Replace("[store_name]", storeName ?? "")
            .Replace("[invoice_number]", invoiceNumber ?? order.InvoiceNumber ?? "")
            .Replace("[document_url]", documentUrl ?? order.CardcomDocumentUrl ?? "")
            .Replace("[payment_url]", paymentUrl ?? "")
            .Replace("[refund_amount]", refundStr);
    }

    /// <summary>
    /// Match picking: if any line has picked qty &gt; 0, sum only those lines (+ shipping). Otherwise sum all line totals.
    /// </summary>
    private static decimal ResolveOrderTotalForPlaceholders(Order order)
    {
        var items = order.OrderItem;
        if (items == null || items.Count == 0)
            return order.Total ?? 0m;

        var anyPicked = items.Any(i => i.PickedQuantity.HasValue && i.PickedQuantity.Value > 0m);
        var shipping = order.ShippingCost ?? 0m;
        if (anyPicked)
        {
            var pickedSum = items.Sum(i =>
            {
                if (!i.PickedQuantity.HasValue || i.PickedQuantity.Value <= 0m)
                    return 0m;
                if (i.TotalPrice.HasValue)
                    return i.TotalPrice.Value;
                return i.PickedQuantity.Value * (i.PricePerUnit ?? 0m);
            });
            return pickedSum + shipping;
        }

        var allLines = items.Sum(i => i.TotalPrice ?? i.Quantity * (i.PricePerUnit ?? 0m));
        if (allLines + shipping > 0m)
            return allLines + shipping;
        return order.Total ?? 0m;
    }
}
