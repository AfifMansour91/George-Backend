using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce when order is paid (invoice, Cardcom clearance, etc.). Auth: X-Api-Key. Identify order with <see cref="OrderNumber"/> and/or <see cref="OrderId"/> (WooCommerce order id).</summary>
public class WooCommerceOrderPaymentPayload
{
    /// <summary>WooCommerce / plugin order number (same as sent on create; preferred).</summary>
    [JsonProperty("orderNumber")]
    public string? OrderNumber { get; set; }

    /// <summary>WooCommerce numeric or string order id; used when <see cref="OrderNumber"/> is omitted.</summary>
    [JsonProperty("orderId")]
    public JToken? OrderId { get; set; }

    /// <summary>Optional; API key already identifies site.</summary>
    [JsonProperty("siteId")]
    public string? SiteId { get; set; }

    [JsonProperty("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonProperty("paymentReference")]
    public string? PaymentReference { get; set; }

    [JsonProperty("clearanceNumber")]
    public string? ClearanceNumber { get; set; }

    [JsonProperty("amount")]
    public decimal? Amount { get; set; }

    [JsonProperty("paidAt")]
    public DateTime? PaidAt { get; set; }

    /// <summary>Cardcom (or gateway) payment details; stored as JSON on the order.</summary>
    [JsonProperty("cardcomPayment")]
    public JToken? CardcomPayment { get; set; }

    /// <summary>Gateway payment outcome (e.g. success, failed). When clearly failed, we do not set <c>PaymentStatus</c> to Paid.</summary>
    [JsonProperty("status")]
    public string? Status { get; set; }

    /// <summary>Resolves external order key for lookup against <see cref="George.DB.Order.ExternalOrderId"/>.</summary>
    public string? ResolveExternalOrderKey()
    {
        if (!string.IsNullOrWhiteSpace(OrderNumber))
            return OrderNumber.Trim();
        if (OrderId == null || OrderId.Type == JTokenType.Null)
            return null;
        return OrderId.Type switch
        {
            JTokenType.String => OrderId.Value<string>()?.Trim(),
            JTokenType.Integer => OrderId.Value<long>().ToString(),
            JTokenType.Float => OrderId.Value<decimal>().ToString(CultureInfo.InvariantCulture),
            _ => OrderId.ToString().Trim()
        };
    }
}
