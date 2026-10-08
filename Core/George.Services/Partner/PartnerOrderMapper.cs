using George.DB;
using George.Services.Response;

namespace George.Services.Partner;

/// <summary>
/// Maps a George <see cref="Order"/> (with its lines and status history) to the partner-facing
/// <see cref="PartnerOrderRes"/>. One mapper for the API responses and the outbound webhooks so both
/// carry the identical shape.
/// </summary>
public static class PartnerOrderMapper
{
    public const string SourceWhatsApp = "WhatsApp";
    public const string SourcePartner = "Partner";

    /// <summary>Orders placed through the Partner API - the only ones partner webhooks are sent for.</summary>
    public static bool IsPartnerSource(string? source)
    {
        var s = source?.Trim();
        return string.Equals(s, SourceWhatsApp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, SourcePartner, StringComparison.OrdinalIgnoreCase);
    }

    public static PartnerOrderRes Map(Order o, IReadOnlyList<(string Status, DateTime OccurredAt)>? history, bool alreadyExisted)
    {
        var lines = (o.OrderItem ?? new List<OrderItem>())
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .ToList();
        // Picked quantities are meaningful only once picking finished; before that George stores a baseline that
        // would read as "already weighed" to the partner.
        var picked = string.Equals(o.Status, "Ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        var discountTotal = lines.Sum(i => i.DiscountAmount ?? 0m)
            + (o.ManualDiscountAmount ?? 0m);

        var res = new PartnerOrderRes
        {
            OrderId = o.Id,
            OrderNumber = o.OrderNumber,
            PartnerRef = o.ExternalOrderId,
            AlreadyExisted = alreadyExisted,
            Source = o.Source,
            Status = o.Status,
            PaymentStatus = o.PaymentStatus,
            PaymentMethod = o.PaymentMethod,
            PaymentSettleStatus = o.PaymentSettleStatus,
            PaidAt = o.PaidAt,
            InvoiceUrl = FirstNonEmpty(o.CardcomDocumentUrl, o.PayPlusDocumentUrl),
            DeliveryType = o.DeliveryType,
            DeliveryDate = o.DeliveryDate,
            DeliveryTime = o.DeliveryTime,
            PickupDate = o.PickupDate,
            PickupTime = o.PickupTime,
            DeliveryAddress = o.DeliveryAddress,
            DeliveryStreet = o.DeliveryStreet,
            DeliveryCity = o.DeliveryCity,
            DeliveryApartment = o.DeliveryApartment,
            DeliveryFloor = o.DeliveryFloor,
            DeliveryEntranceCode = o.DeliveryEntranceCode,
            DeliveryTrackingLink = FirstNonEmpty(o.DeliveryProviderTrackingLink, o.WoltTrackingUrl),
            DeliveryStatus = FirstNonEmpty(o.DeliveryProviderStatus, o.WoltStatus),
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            CustomerNote = o.CustomerNote,
            CouponCode = o.CouponCode,
            SubTotal = o.SubTotal,
            DiscountTotal = Math.Round(discountTotal, 2, MidpointRounding.AwayFromZero),
            ShippingCost = o.ShippingCost,
            Total = o.Total,
            BagsCount = o.BagsCount,
            CanCancel = string.Equals(o.Status, "New", StringComparison.OrdinalIgnoreCase),
            CreatedAt = AsUtc(o.CreationTime),
            UpdatedAt = o.UpdatedDate.HasValue ? AsUtc(o.UpdatedDate.Value) : null,
            Items = lines
                .Where(i => !BundleOrderLines.IsBundleChild(i))
                .Select(i => MapItem(i, picked))
                .ToList(),
        };

        if (history != null)
        {
            foreach (var (status, at) in history)
            {
                var utc = AsUtc(at);
                switch (status?.Trim().ToLowerInvariant())
                {
                    case "intreatment": res.InTreatmentAt ??= utc; break;
                    case "ready": res.ReadyAt ??= utc; break;
                    case "completed": res.CompletedAt ??= utc; break;
                    case "cancelled": res.CancelledAt ??= utc; break;
                }
            }
        }
        if (res.CancelledAt == null && string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            res.CancelledAt = res.UpdatedAt ?? res.CreatedAt;
        return res;
    }

    public static PartnerOrderItemRes MapItem(OrderItem i) => MapItem(i, includePicked: true);

    public static PartnerOrderItemRes MapItem(OrderItem i, bool includePicked) => new()
    {
        OrderItemId = i.Id,
        ProductId = i.ProductId,
        ProductVariantId = i.ProductVariantId,
        Sku = i.LineSku,
        Title = i.Title,
        VariantTitle = i.VariantTitle,
        Quantity = i.Quantity,
        PickedQuantity = includePicked ? i.PickedQuantity : null,
        PricePerUnit = i.PricePerUnit,
        TotalPrice = i.TotalPrice,
        DiscountAmount = i.DiscountAmount,
        Notes = i.Notes,
    };

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static DateTime AsUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
