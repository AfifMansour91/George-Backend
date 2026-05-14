using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace George.Services.Request;

/// <summary>Gateway block from WooCommerce root <c>payment</c>.</summary>
public class WooCommerceOrderPaymentGatewayDetails
{
    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }

    [JsonProperty("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonProperty("paymentGateway")]
    public string? PaymentGateway { get; set; }
}

/// <summary>Payload from WooCommerce <c>POST /WooCommerce/OrderPayment</c>. Auth: X-Api-Key. Order: <see cref="OrderNumber"/> / <see cref="OrderId"/> / <see cref="ExternalOrderId"/> vs <see cref="George.DB.Order.ExternalOrderId"/>.</summary>
public class WooCommerceOrderPaymentPayload
{
    [JsonProperty("orderNumber")]
    public string? OrderNumber { get; set; }

    [JsonProperty("orderId")]
    public JToken? OrderId { get; set; }

    [JsonProperty("externalOrderId")]
    public JToken? ExternalOrderId { get; set; }

    /// <summary>Optional echo; site is taken from API key.</summary>
    [JsonProperty("siteId")]
    public string? SiteId { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("isFinished")]
    public string? IsFinished { get; set; }

    [JsonProperty("payment")]
    public WooCommerceOrderPaymentGatewayDetails? Payment { get; set; }

    // -------------------------------------------------------------------------
    // Populated during normalize from payment.* — not deserialized from JSON.
    // -------------------------------------------------------------------------
    [JsonIgnore]
    public string? InvoiceNumber { get; set; }

    [JsonIgnore]
    public string? PaymentReference { get; set; }

    /// <summary>Applies root plugin fields into the internal flat fields used downstream.</summary>
    public void NormalizeWooCommercePaymentRequest() => ApplyRootFlatPluginShape();

    /// <summary>Root <c>payment</c>, <c>isFinished</c>, trims.</summary>
    public void ApplyRootFlatPluginShape()
    {
        if (!string.IsNullOrWhiteSpace(Status))
            Status = Status!.Trim();
        if (!string.IsNullOrWhiteSpace(SiteId))
            SiteId = SiteId.Trim();
        if (!string.IsNullOrWhiteSpace(OrderNumber))
            OrderNumber = OrderNumber.Trim();
        if (Payment == null)
            return;
        if (!string.IsNullOrWhiteSpace(Payment.InvoiceNumber))
            InvoiceNumber = Payment.InvoiceNumber.Trim();
        if (!string.IsNullOrWhiteSpace(Payment.TransactionId))
            PaymentReference = Payment.TransactionId.Trim();
    }

    /// <summary>When root <see cref="Payment"/> is present, a non-empty <c>transactionId</c> is required to treat the order as paid.</summary>
    public bool RequiresGatewayTransactionIdForPaid() => Payment != null;

    public string? ResolveGatewayTransactionIdForPaid() =>
        string.IsNullOrWhiteSpace(Payment?.TransactionId) ? null : Payment!.TransactionId.Trim();

    /// <summary>Resolves external order key for lookup against <see cref="George.DB.Order.ExternalOrderId"/>.</summary>
    public string? ResolveExternalOrderKey()
    {
        if (!string.IsNullOrWhiteSpace(OrderNumber))
            return OrderNumber.Trim();
        var fromOrderId = GatewayTokenToString(OrderId);
        if (!string.IsNullOrWhiteSpace(fromOrderId))
            return fromOrderId;
        var fromExt = GatewayTokenToString(ExternalOrderId);
        if (!string.IsNullOrWhiteSpace(fromExt))
            return fromExt;
        return null;
    }

    /// <summary>Formats JSON <c>orderId</c> / <c>externalOrderId</c> tokens for storage or lookup.</summary>
    public static string? GatewayTokenToString(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;
        var s = token.Type switch
        {
            JTokenType.String => token.Value<string>()?.Trim(),
            JTokenType.Integer => token.Value<long>().ToString(CultureInfo.InvariantCulture),
            JTokenType.Float => token.Value<decimal>().ToString(CultureInfo.InvariantCulture),
            _ => token.ToString().Trim()
        };
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
