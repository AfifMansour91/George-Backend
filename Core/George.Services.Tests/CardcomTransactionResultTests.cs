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
}
