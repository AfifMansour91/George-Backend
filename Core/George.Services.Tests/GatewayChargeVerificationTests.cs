using George.Services.Payments;
using Xunit;

namespace George.Services.Tests;

public class GatewayChargeVerificationTests
{
    private static CardcomTransactionInfoResult FinalCharge(decimal amount) => new()
    {
        Success = true,
        ResponseCode = 0,
        Amount = amount,
        IsFinalCharge = true,
        IsAuthorizationHold = false,
    };

    [Fact]
    public void Final_charge_matching_order_total_is_match()
    {
        var outcome = GatewayChargeVerification.Evaluate(FinalCharge(325m), 325m, null);
        Assert.Equal(GatewayVerifyOutcome.Match, outcome);
    }

    [Fact]
    public void Delinka_case_charge_above_order_total_is_mismatch()
    {
        // Order #48: giorgio total 325, Cardcom actually charged 380.
        var outcome = GatewayChargeVerification.Evaluate(FinalCharge(380m), 325m, null);
        Assert.Equal(GatewayVerifyOutcome.Mismatch, outcome);
    }

    [Fact]
    public void Rounding_within_tolerance_is_match()
    {
        var outcome = GatewayChargeVerification.Evaluate(FinalCharge(325.01m), 325m, null);
        Assert.Equal(GatewayVerifyOutcome.Match, outcome);
    }

    [Fact]
    public void Partial_refund_after_correct_charge_stays_match()
    {
        // Charge equals the total at charge time; a later 55 refund is a separate transaction
        // and must NOT flip the original charge to a mismatch.
        var outcome = GatewayChargeVerification.Evaluate(FinalCharge(325m), 325m, 55m);
        Assert.Equal(GatewayVerifyOutcome.Match, outcome);
    }

    [Fact]
    public void Authorization_hold_is_hold_only_and_not_flagged()
    {
        var info = new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = 0,
            Amount = 325m,
            IsFinalCharge = false,
            IsAuthorizationHold = true,
        };
        Assert.Equal(GatewayVerifyOutcome.HoldOnly, GatewayChargeVerification.Evaluate(info, 325m, null));
    }

    [Fact]
    public void Refunded_deal_with_covering_george_refund_is_match()
    {
        var info = new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = 0,
            Amount = 325m,
            IsRefund = true,
        };
        Assert.Equal(GatewayVerifyOutcome.Match, GatewayChargeVerification.Evaluate(info, 325m, 325m));
    }

    [Fact]
    public void Refunded_deal_george_never_recorded_is_mismatch()
    {
        var info = new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = 0,
            Amount = 325m,
            IsRefund = true,
        };
        Assert.Equal(GatewayVerifyOutcome.Mismatch, GatewayChargeVerification.Evaluate(info, 325m, null));
    }

    [Fact]
    public void Failed_inquiry_is_inconclusive()
    {
        var info = new CardcomTransactionInfoResult { Success = false, ResponseCode = -1 };
        Assert.Equal(GatewayVerifyOutcome.Inconclusive, GatewayChargeVerification.Evaluate(info, 325m, null));
    }

    [Fact]
    public void Information_row_is_inconclusive()
    {
        var info = new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = 0,
            Amount = 325m,
            DealType = "Information",
            IsFinalCharge = false,
        };
        Assert.Equal(GatewayVerifyOutcome.Inconclusive, GatewayChargeVerification.Evaluate(info, 325m, null));
    }

    [Fact]
    public void Missing_amount_or_total_is_inconclusive()
    {
        Assert.Equal(GatewayVerifyOutcome.Inconclusive, GatewayChargeVerification.Evaluate(FinalCharge(0m), 325m, null));
        Assert.Equal(GatewayVerifyOutcome.Inconclusive, GatewayChargeVerification.Evaluate(FinalCharge(325m), null, null));
    }
}
