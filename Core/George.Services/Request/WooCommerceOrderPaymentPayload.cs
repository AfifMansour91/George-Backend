using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace George.Services.Request;

/// <summary>Gateway block from WooCommerce root <c>payment</c> (OrderPayment webhook or embedded in Order payload).</summary>
public class WooCommerceOrderPaymentGatewayDetails
{
    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }

    [JsonProperty("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonProperty("paymentGateway")]
    public string? PaymentGateway { get; set; }

    [JsonProperty("authorizedAmount")]
    public decimal? AuthorizedAmount { get; set; }

    [JsonProperty("amount")]
    public decimal? Amount { get; set; }

    [JsonProperty("last4Digits")]
    public string? Last4Digits { get; set; }

    [JsonProperty("cardLast4")]
    public string? CardLast4 { get; set; }

    [JsonProperty("cardBrand")]
    public string? CardBrand { get; set; }

    [JsonProperty("approvalNumber")]
    public string? ApprovalNumber { get; set; }

    public string? ResolveTransactionId() =>
        string.IsNullOrWhiteSpace(TransactionId) ? null : TransactionId.Trim();

    public string? ResolveLast4Digits()
    {
        var a = Last4Digits?.Trim();
        if (!string.IsNullOrWhiteSpace(a)) return a;
        var b = CardLast4?.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b;
    }

    public decimal? ResolveAuthOrPaymentAmount() =>
        AuthorizedAmount is > 0 ? AuthorizedAmount : Amount is > 0 ? Amount : null;

    public string? ResolvePaymentGatewayId()
    {
        var g = PaymentGateway?.Trim();
        if (string.IsNullOrWhiteSpace(g)) return null;
        if (g.Contains("cardcom", StringComparison.OrdinalIgnoreCase)) return "cardcom";
        return g.ToLowerInvariant();
    }
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

    /// <summary>When root <see cref="Payment"/> is present, a non-empty <c>transactionId</c> is required for gateway success.</summary>
    public bool RequiresGatewayTransactionIdForPaid() => Payment != null;

    public string? ResolveGatewayTransactionIdForPaid() => Payment?.ResolveTransactionId();

    /// <summary>Website checkout: J5 auth hold (default). Final charge sends <c>isFinished</c> / status indicating capture.</summary>
    public bool IsFinalGatewayCapture() =>
        WooCommerceGatewayPaymentInterpreter.IsFinalCapture(IsFinished, Status);

    public bool IsGatewayFailure() =>
        WooCommerceGatewayPaymentInterpreter.IsGatewayFailureStatus(Status);

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

/// <summary>Interpret WooCommerce gateway webhook fields for auth-hold vs final capture.</summary>
public static class WooCommerceGatewayPaymentInterpreter
{
    public static bool IsGatewayFailureStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim().ToLowerInvariant();
        if (s is "failed" or "fail" or "error" or "declined" or "rejected" or "cancelled" or "canceled") return true;
        return s.Contains("declin", StringComparison.Ordinal) || s.Contains("fail", StringComparison.Ordinal);
    }

    public static bool IsGatewaySuccessStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return true;
        return !IsGatewayFailureStatus(status);
    }

  /// <summary>True when gateway reports final charge (not J5 hold at checkout).</summary>
    public static bool IsFinalCapture(string? isFinished, string? gatewayStatus)
    {
        if (!string.IsNullOrWhiteSpace(isFinished))
        {
            var f = isFinished.Trim().ToLowerInvariant();
            if (f is "true" or "1" or "yes" or "charged" or "completed" or "finished" or "paid"
                or "capture" or "captured" or "done")
                return true;
            if (f is "false" or "0" or "no" or "authorized" or "auth" or "hold" or "pending"
                or "j5" or "validate")
                return false;
        }

        if (!string.IsNullOrWhiteSpace(gatewayStatus))
        {
            var s = gatewayStatus.Trim().ToLowerInvariant();
            if (s.Contains("captur", StringComparison.Ordinal) || s is "paid" or "completed" or "charged")
                return true;
            if (s.Contains("auth", StringComparison.Ordinal) || s is "authorized" or "hold" or "j5")
                return false;
        }

        return false;
    }
}
