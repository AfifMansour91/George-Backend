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
    // Not part of the current WooCommerce JSON contract — kept for
    // OrderService / merge pipeline (populated from payment.* or left unset).
    // -------------------------------------------------------------------------
    [JsonIgnore]
    public string? InvoiceNumber { get; set; }

    [JsonIgnore]
    public string? PaymentReference { get; set; }

    [JsonIgnore]
    public string? ClearanceNumber { get; set; }

    [JsonIgnore]
    public decimal? Amount { get; set; }

    [JsonIgnore]
    public DateTime? PaidAt { get; set; }

    [JsonIgnore]
    public JToken? CardcomPayment { get; set; }

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
        MergeGatewayPaymentIntoSelf(Payment, IsFinished);
    }

    private void MergeGatewayPaymentIntoSelf(WooCommerceOrderPaymentGatewayDetails? p, string? isFinished)
    {
        if (p != null)
        {
            if (!string.IsNullOrWhiteSpace(p.InvoiceNumber))
                InvoiceNumber = p.InvoiceNumber.Trim();
            if (!string.IsNullOrWhiteSpace(p.TransactionId))
                PaymentReference = p.TransactionId.Trim();
        }

        var hasGatewayFields = p != null &&
            (!string.IsNullOrWhiteSpace(p.TransactionId) ||
             !string.IsNullOrWhiteSpace(p.InvoiceNumber) ||
             !string.IsNullOrWhiteSpace(p.PaymentGateway));
        var hasIsFinished = !string.IsNullOrWhiteSpace(isFinished);

        if (!hasGatewayFields && !hasIsFinished)
            return;

        if (CardcomPayment is JObject joExisting)
        {
            if (p != null)
            {
                if (!string.IsNullOrWhiteSpace(p.TransactionId)) joExisting["transactionId"] = p.TransactionId.Trim();
                if (!string.IsNullOrWhiteSpace(p.InvoiceNumber)) joExisting["invoiceNumber"] = p.InvoiceNumber.Trim();
                if (!string.IsNullOrWhiteSpace(p.PaymentGateway)) joExisting["paymentGateway"] = p.PaymentGateway.Trim();
            }
            if (hasIsFinished)
                joExisting["isFinished"] = isFinished!.Trim();
            return;
        }

        if (CardcomPayment == null || CardcomPayment.Type == JTokenType.Null)
        {
            var jo = new JObject();
            if (p != null)
            {
                if (!string.IsNullOrWhiteSpace(p.TransactionId)) jo["transactionId"] = p.TransactionId.Trim();
                if (!string.IsNullOrWhiteSpace(p.InvoiceNumber)) jo["invoiceNumber"] = p.InvoiceNumber.Trim();
                if (!string.IsNullOrWhiteSpace(p.PaymentGateway)) jo["paymentGateway"] = p.PaymentGateway.Trim();
            }
            if (hasIsFinished)
                jo["isFinished"] = isFinished!.Trim();
            if (jo.Count > 0)
                CardcomPayment = jo;
            return;
        }

        if (hasGatewayFields || hasIsFinished)
        {
            var wrap = new JObject { ["raw"] = CardcomPayment };
            if (p != null)
            {
                if (!string.IsNullOrWhiteSpace(p.TransactionId)) wrap["transactionId"] = p.TransactionId.Trim();
                if (!string.IsNullOrWhiteSpace(p.InvoiceNumber)) wrap["invoiceNumber"] = p.InvoiceNumber.Trim();
                if (!string.IsNullOrWhiteSpace(p.PaymentGateway)) wrap["paymentGateway"] = p.PaymentGateway.Trim();
            }
            if (hasIsFinished)
                wrap["isFinished"] = isFinished!.Trim();
            CardcomPayment = wrap;
        }
    }

    /// <summary>When root <see cref="Payment"/> is present, a non-empty <c>transactionId</c> is required to mark paid.</summary>
    public bool RequiresGatewayTransactionIdForPaid() => Payment != null;

    public string? ResolveGatewayTransactionIdForPaid() =>
        string.IsNullOrWhiteSpace(Payment?.TransactionId) ? null : Payment!.TransactionId.Trim();

    /// <summary>Resolves external order key for lookup against <see cref="George.DB.Order.ExternalOrderId"/>.</summary>
    public string? ResolveExternalOrderKey()
    {
        if (!string.IsNullOrWhiteSpace(OrderNumber))
            return OrderNumber.Trim();
        if (FormatExternalToken(OrderId, out var fromOrderId))
            return fromOrderId;
        if (FormatExternalToken(ExternalOrderId, out var fromExt))
            return fromExt;
        return null;
    }

    private static bool FormatExternalToken(JToken? token, out string? key)
    {
        key = null;
        if (token == null || token.Type == JTokenType.Null)
            return false;
        var s = token.Type switch
        {
            JTokenType.String => token.Value<string>()?.Trim(),
            JTokenType.Integer => token.Value<long>().ToString(CultureInfo.InvariantCulture),
            JTokenType.Float => token.Value<decimal>().ToString(CultureInfo.InvariantCulture),
            _ => token.ToString().Trim()
        };
        if (string.IsNullOrWhiteSpace(s))
            return false;
        key = s;
        return true;
    }
}
