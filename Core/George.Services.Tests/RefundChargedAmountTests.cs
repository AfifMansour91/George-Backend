using George.DB;
using George.Services.Payments;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// PEPE #20 (23/9): the card was charged 384.85 (the total the picking page sent at finish), a later line
/// re-save moved Order.Total to 384.86, and the cancel refund asked PayPlus for 384.86 -
/// "amount-bigger-that-original-transaction", customer never refunded. Refunds now draw on the charge trail.
/// </summary>
public class RefundChargedAmountTests
{
    private static OrderPaymentEvent Ev(string type, string status, decimal? amount, string? txId, int minutesAgo) => new()
    {
        EventType = type,
        Provider = "payplus",
        StatusCode = status,
        Amount = amount,
        GatewayTransactionId = txId,
        CreationTime = DateTime.UtcNow.AddMinutes(-minutesAgo),
    };

    [Fact]
    public void PayPlusHoldThenCapture_UsesTheCaptureAmount_NotTheHoldOrHeader()
    {
        var order = new Order
        {
            Total = 384.86m,
            GatewayPaymentTransactionId = "ee583617-b4c3",
            PayPlusTransactionUid = "7213f7cc-d8d6",
        };
        // Newest first, as PaymentStorage.GetPaymentEventsAsync returns them.
        var events = new[]
        {
            Ev("Refund", "1", 384.86m, null, 0),
            Ev("CreateDocument", "-1", 384.85m, null, 1),
            Ev("ReleaseHold", "0", 208.72m, "7213f7cc-d8d6", 2),
            Ev("CaptureAuthorization", "0", 384.85m, "ee583617-b4c3", 3),
            Ev("ValidateReturn", "0", 593.57m, "7213f7cc-d8d6", 5),
            Ev("InitHostedSession", "0", 593.57m, null, 6),
        };

        Assert.Equal(384.85m, PaymentService.ResolveChargedAmountFromEvents(order, events));
    }

    [Fact]
    public void ChargeNowHostedSession_MatchesTheReturnEventByTransactionId()
    {
        var order = new Order { Total = 120.00m, GatewayPaymentTransactionId = "abc", PaymentReference = "abc" };
        var events = new[]
        {
            Ev("ValidateReturn", "0", 119.99m, "abc", 1),
            Ev("InitHostedSession", "0", 119.99m, null, 2),
        };

        Assert.Equal(119.99m, PaymentService.ResolveChargedAmountFromEvents(order, events));
    }

    [Fact]
    public void WithoutTransactionMatch_HostedHoldEventIsNotTrusted_CaptureIs()
    {
        // Cardcom J5: the capture lives under a new tx id the order may not carry (legacy rows).
        var order = new Order { Total = 200m, GatewayPaymentTransactionId = null, PaymentReference = null };
        var events = new[]
        {
            Ev("CaptureAuthorization", "0", 199.90m, "999", 1),
            Ev("ValidateCallback", "0", 240.00m, "111", 5),
        };

        Assert.Equal(199.90m, PaymentService.ResolveChargedAmountFromEvents(order, events));
    }

    [Fact]
    public void FailedChargeEventsAreIgnored_FallsBackToVerifiedAmountThenNull()
    {
        var order = new Order { Total = 50m, GatewayVerifiedAmount = 49.90m };
        var events = new[] { Ev("CaptureAuthorization", "-1", 50m, "x", 1) };
        Assert.Equal(49.90m, PaymentService.ResolveChargedAmountFromEvents(order, events));

        var manualPaid = new Order { Total = 50m };
        Assert.Null(PaymentService.ResolveChargedAmountFromEvents(manualPaid, Array.Empty<OrderPaymentEvent>()));
    }

    [Fact]
    public void RefundOrDocumentEventsNeverCountAsCharges()
    {
        var order = new Order { Total = 100m, GatewayPaymentTransactionId = "r1" };
        var events = new[]
        {
            Ev("Refund", "0", 100m, "r1", 1),
            Ev("CreateRefundDocument", "0", 100m, "r1", 1),
            Ev("Void", "0", 100m, "r1", 1),
        };

        Assert.Null(PaymentService.ResolveChargedAmountFromEvents(order, events));
    }
}
