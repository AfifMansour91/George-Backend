using George.Services.Payments.Cardcom;

namespace George.Services.Tests;

public class CardcomTransactionResultTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(700, true)]
    [InlineData(701, true)]
    [InlineData(1, false)]
    [InlineData(60000042, false)]
    public void IsCardcomTransactionResponseSuccess_matches_cardcom_j_codes(int code, bool expected) =>
        Assert.Equal(expected, CardcomGateway.IsCardcomTransactionResponseSuccess(code));

    // Order 6321 (2026-08-20): Cardcom answered GetTransactionInfoById with the body "\"\"" and the
    // parser read it as a successful final charge, marking an uncharged order Paid.
    [Theory]
    [InlineData("\"\"")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Description\":\"no code\"}")]
    [InlineData("not json at all")]
    public void ParseTransactionInfoResult_rejects_bodies_without_response_code(string body)
    {
        var result = CardcomGateway.ParseTransactionInfoResult(body);
        Assert.False(result.Success);
        Assert.False(result.IsFinalCharge);
        Assert.False(result.IsAuthorizationHold);
        Assert.Equal(-1, result.ResponseCode);
    }

    [Fact]
    public void ParseTransactionInfoResult_still_reads_final_charge()
    {
        var result = CardcomGateway.ParseTransactionInfoResult(
            "{\"ResponseCode\":0,\"DealType\":\"Debit\",\"Amount\":627.40,\"TranzactionId\":\"259462076\"}");
        Assert.True(result.Success);
        Assert.True(result.IsFinalCharge);
        Assert.False(result.IsAuthorizationHold);
        Assert.Equal(627.40m, result.Amount);
    }

    [Fact]
    public void ParseTransactionInfoResult_still_reads_authorization_hold()
    {
        var result = CardcomGateway.ParseTransactionInfoResult(
            "{\"ResponseCode\":700,\"DealType\":\"Information\",\"Amount\":546.50}");
        Assert.True(result.Success);
        Assert.False(result.IsFinalCharge);
        Assert.True(result.IsAuthorizationHold);
        Assert.Equal(546.50m, result.Amount);
    }

    [Fact]
    public void ParseTransactionInfoResult_reads_j5_hold_from_j_parameter()
    {
        var result = CardcomGateway.ParseTransactionInfoResult(
            "{\"ResponseCode\":0,\"JParameter\":5,\"Amount\":546.50}");
        Assert.True(result.IsAuthorizationHold);
        Assert.False(result.IsFinalCharge);
    }

    [Fact]
    public void ParseTransactionInfoResult_refund_is_not_a_final_charge()
    {
        var result = CardcomGateway.ParseTransactionInfoResult(
            "{\"ResponseCode\":0,\"DealType\":\"Debit\",\"IsRefund\":true,\"Amount\":100}");
        Assert.False(result.IsFinalCharge);
        Assert.Equal(true, result.IsRefund);
    }
}
