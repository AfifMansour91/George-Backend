using George.Common.Payment;
using George.Services.Request;
using Newtonsoft.Json;

namespace George.Services.Tests;

/// <summary>
/// giorgio "Giorgio charges Cardcom" mode: the checkout token travels in the payment block of the
/// order payload / OrderPayment webhook and Giorgio charges at picking.
/// </summary>
public class WooGiorgioCaptureHandoverTests
{
    [Fact]
    public void Handover_requires_owner_token_and_expiry()
    {
        var full = new WooCommerceOrderPaymentGatewayDetails
        {
            CaptureOwner = "Giorgio", Token = "11111111-2222-3333-4444-555555555555", TokenExpiry = "1228",
        };
        Assert.True(full.IsGiorgioCaptureHandover());

        Assert.False(new WooCommerceOrderPaymentGatewayDetails
        {
            Token = full.Token, TokenExpiry = full.TokenExpiry,
        }.IsGiorgioCaptureHandover());
        Assert.False(new WooCommerceOrderPaymentGatewayDetails
        {
            CaptureOwner = "Giorgio", TokenExpiry = full.TokenExpiry,
        }.IsGiorgioCaptureHandover());
        Assert.False(new WooCommerceOrderPaymentGatewayDetails
        {
            CaptureOwner = "Giorgio", Token = full.Token,
        }.IsGiorgioCaptureHandover());
    }

    [Theory]
    [InlineData("1228", "1228")]
    [InlineData("12/28", "1228")]
    [InlineData("122028", "1228")]
    [InlineData("12/2028", "1228")]
    [InlineData("0330", "0330")]
    // QA order 1982: the gateway's cardcom_Tokef carried an unpadded single-digit month.
    [InlineData("3 29", "0329")]
    [InlineData("1/28", "0128")]
    [InlineData("", null)]
    [InlineData("12", null)]
    public void Token_expiry_normalizes_to_MMYY(string raw, string? expected)
    {
        var details = new WooCommerceOrderPaymentGatewayDetails { TokenExpiry = raw };
        Assert.Equal(expected, details.ResolveTokenExpiryMMYY());
    }

    [Fact]
    public void Plugin_payload_deserializes_handover_fields()
    {
        const string json = """
            {"transactionId":"259655688","paymentGateway":"cardcom","authorizedAmount":90.0,
             "captureOwner":"Giorgio","token":"11111111-2222-3333-4444-555555555555",
             "tokenExpiry":"0427","approvalNumber":"0012345","numOfPayments":3}
            """;
        var details = JsonConvert.DeserializeObject<WooCommerceOrderPaymentGatewayDetails>(json)!;
        Assert.True(details.IsGiorgioCaptureHandover());
        Assert.Equal("0427", details.ResolveTokenExpiryMMYY());
        Assert.Equal(3, details.NumOfPayments);
        Assert.Equal("0012345", details.ApprovalNumber);
        Assert.Equal("259655688", details.ResolveTransactionId());
    }

    [Theory]
    [InlineData("Giorgio", true)]
    [InlineData("giorgio", true)]
    [InlineData(" Giorgio ", true)]
    [InlineData("Plugin", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PaymentCaptureOwner_IsGiorgio(string? value, bool expected) =>
        Assert.Equal(expected, PaymentCaptureOwner.IsGiorgio(value));
}
