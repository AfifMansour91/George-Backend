using George.Services.Payments.Cardcom;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

public class CardcomCardDisplayExtractionTests
{
    private static CardcomGateway CreateGateway() =>
        new(NullHttpClientFactory.Instance, NullLogger<CardcomGateway>.Instance);

    [Fact]
    public void ExtractCardDisplayFields_ReadsLast4BrandAndTokenEx_FromValidateCallbackJson()
    {
        const string json = """
            {
              "ResponseCode": 0,
              "TokenInfo": {
                "Token": "e0671ea9-1156-4e7b-8767-37bab156539d",
                "TokenExDate": "20281101",
                "CardYear": 2028,
                "CardMonth": 10
              },
              "TranzactionInfo": {
                "Last4CardDigits": 8,
                "Last4CardDigitsString": "0008",
                "Brand": "Visa",
                "CardName": "ויזה זהב"
              }
            }
            """;

        var display = CreateGateway().ExtractCardDisplayFields(json);

        Assert.Equal("0008", display.Last4Digits);
        Assert.Equal("Visa", display.CardBrand);
        Assert.Equal("20281101", display.TokenExDate);
        Assert.Equal("1028", display.CardExpirationMMYY);
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public static readonly NullHttpClientFactory Instance = new();
        public HttpClient CreateClient(string name) => new();
    }
}
