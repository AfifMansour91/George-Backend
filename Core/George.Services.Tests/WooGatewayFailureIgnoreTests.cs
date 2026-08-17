using George.Common.Payment;
using George.Services.Request;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// <see cref="WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure"/> — a transient/stale
/// gateway "failed" report must not flip an authorized/settled order (order 6042: "failed" webhook
/// arrived one second before the capture success and showed "charge failed" for a paid order).
/// </summary>
public class WooGatewayFailureIgnoreTests
{
    [Fact]
    public void Bare_failed_on_authorized_hold_is_ignored()
    {
        // Old plugin mid-capture echo: status=failed, no tx, no failureReason, J5 hold active.
        Assert.True(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            PaymentSettleStatus.Authorized, "failed", null, null));
    }

    [Theory]
    [InlineData(PaymentSettleStatus.Captured)]
    [InlineData(PaymentSettleStatus.PartiallyCaptured)]
    [InlineData(PaymentSettleStatus.Refunded)]
    [InlineData(PaymentSettleStatus.PartiallyRefunded)]
    public void Any_failed_on_settled_order_is_ignored(string settleStatus)
    {
        // Late/racing failure after the money moved — even with tx or explicit reason.
        Assert.True(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            settleStatus, "failed", "259180772", "cardcom_capture_not_confirmed"));
        Assert.True(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            settleStatus, "failed", null, null));
    }

    [Fact]
    public void Explicit_capture_failure_on_authorized_hold_is_applied()
    {
        // New plugin genuine capture failure: failureReason present — must NOT be ignored.
        Assert.False(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            PaymentSettleStatus.Authorized, "failed", null, "cardcom_capture_not_confirmed"));
    }

    [Fact]
    public void Failed_with_transaction_id_on_authorized_hold_is_applied()
    {
        Assert.False(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            PaymentSettleStatus.Authorized, "failed", "259150105", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(PaymentSettleStatus.None)]
    [InlineData(PaymentSettleStatus.Initiated)]
    [InlineData(PaymentSettleStatus.Failed)]
    public void Failed_before_any_hold_is_applied(string? settleStatus)
    {
        // Checkout decline (no authorization yet) must still surface.
        Assert.False(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            settleStatus, "failed", null, null));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("authorized")]
    [InlineData(null)]
    public void Non_failure_status_is_never_ignored(string? gatewayStatus)
    {
        Assert.False(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            PaymentSettleStatus.Authorized, gatewayStatus, null, null));
        Assert.False(WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
            PaymentSettleStatus.Captured, gatewayStatus, null, null));
    }
}
