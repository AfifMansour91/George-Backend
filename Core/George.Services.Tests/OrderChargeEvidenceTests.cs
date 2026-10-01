using George.Common.Payment;
using George.DB;
using George.Services.Payments;

namespace George.Services.Tests;

/// <summary>
/// "Paid" is a client flag, not a charge (Hinnawi Jaffa #76/#79/#80, Sept 2026: delivered, reported as income,
/// never charged). These pin what counts as charge evidence for the update guard and the revenue report.
/// </summary>
public class OrderChargeEvidenceTests
{
    private static Order PaidPhoneCredit(string settle, string method = "CreditPhone") => new()
    {
        Id = 1,
        PaymentStatus = "Paid",
        PaymentMethod = method,
        PaymentGateway = PaymentGatewayProviderId.Cardcom,
        PaymentSettleStatus = settle,
    };

    [Theory]
    [InlineData(PaymentSettleStatus.Initiated)]
    [InlineData(PaymentSettleStatus.Authorized)]
    [InlineData(PaymentSettleStatus.Failed)]
    [InlineData(PaymentSettleStatus.Voided)]
    public void PaidFlag_OnGatewayCreditOrder_WithoutChargeEvidence_IsPaidWithoutCharge(string settle)
    {
        Assert.True(OrderChargeEvidence.IsPaidWithoutCharge(PaidPhoneCredit(settle), hasSuccessfulChargeEvent: false));
    }

    [Theory]
    [InlineData(PaymentSettleStatus.Captured)]
    [InlineData(PaymentSettleStatus.PartiallyCaptured)]
    [InlineData(PaymentSettleStatus.Refunded)]
    [InlineData(PaymentSettleStatus.PartiallyRefunded)]
    public void SettledGatewayState_IsChargeEvidence_EvenWithoutGeorgeEvents(string settle)
    {
        // Woo orders captured by the store plugin reach Captured through the webhook with no George-side event.
        Assert.False(OrderChargeEvidence.IsPaidWithoutCharge(PaidPhoneCredit(settle), hasSuccessfulChargeEvent: false));
    }

    [Fact]
    public void SuccessfulChargeEvent_IsChargeEvidence_EvenWhenSettleStatusStayedAuthorized()
    {
        // Hinnawi #77/#78: ChargeToken succeeded but PaymentSettleStatus stayed "Authorized".
        Assert.False(OrderChargeEvidence.IsPaidWithoutCharge(PaidPhoneCredit(PaymentSettleStatus.Authorized), hasSuccessfulChargeEvent: true));
    }

    [Theory]
    [InlineData("Cash")]
    [InlineData("ExternalCredit")]
    [InlineData("OnAccount")]
    [InlineData("BankTransfer")]
    public void CashLikeMethods_AreNeverFlagged(string method)
    {
        var o = PaidPhoneCredit(PaymentSettleStatus.Initiated, method);
        Assert.False(OrderChargeEvidence.IsGatewayCreditOrder(o));
        Assert.False(OrderChargeEvidence.IsPaidWithoutCharge(o, hasSuccessfulChargeEvent: false));
    }

    [Fact]
    public void CreditMethodWithoutGeorgeGateway_IsNotFlagged()
    {
        // A Woo order paid through a gateway George does not integrate: nothing to verify against.
        var o = PaidPhoneCredit(PaymentSettleStatus.Initiated, "CreditCard");
        o.PaymentGateway = null;
        Assert.False(OrderChargeEvidence.IsPaidWithoutCharge(o, hasSuccessfulChargeEvent: false));
    }

    [Fact]
    public void UnpaidOrder_IsNotFlagged()
    {
        var o = PaidPhoneCredit(PaymentSettleStatus.Initiated);
        o.PaymentStatus = "Unpaid";
        Assert.False(OrderChargeEvidence.IsPaidWithoutCharge(o, hasSuccessfulChargeEvent: false));
    }

    [Theory]
    [InlineData("ChargeToken", "0", true)]
    [InlineData("CaptureAuthorization", "000", true)]
    [InlineData("ChargeToken", "Success", true)]
    [InlineData("ChargeToken", "60000004", false)]      // CAL decline
    [InlineData("TokenAuthorizationHold", "0", false)]   // a hold is not a charge
    [InlineData("InitHostedSession", "0", false)]        // an opened payment page is not a charge
    [InlineData("ValidateCallback", "5119", false)]      // "עסקה ממתינה או לא הושלמה"
    public void OnlySuccessfulChargeOrCaptureEvents_Count(string type, string code, bool expected)
    {
        Assert.Equal(expected, OrderChargeEvidence.IsSuccessfulChargeEvent(new OrderPaymentEvent { EventType = type, StatusCode = code }));
    }
}
