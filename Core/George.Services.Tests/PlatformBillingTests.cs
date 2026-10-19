using George.Services.Marketing;
using George.Services.Payments.Cardcom;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace George.Services.Tests;

/// <summary>Self-service SMS packages: price rules (מסלולי Giorgio - prices before VAT) and the platform terminal config.</summary>
public class PlatformBillingTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value)).Build();

    [Theory]
    [InlineData(199, 18, 234.82)]
    [InlineData(349, 18, 411.82)]
    [InlineData(590, 18, 696.20)]
    [InlineData(1290, 18, 1522.20)]
    [InlineData(3990, 18, 4708.20)]
    [InlineData(100, 17, 117.00)]
    [InlineData(100, 0, 100.00)]
    public void Total_adds_vat_rounded_to_agorot(decimal price, decimal vat, decimal expected) =>
        Assert.Equal(expected, PlatformBilling.TotalWithVat(price, vat));

    [Theory]
    [InlineData(199, 2500, 8.0)]
    [InlineData(349, 5000, 7.0)]
    [InlineData(590, 10000, 5.9)]
    [InlineData(1290, 25000, 5.2)]
    [InlineData(2290, 50000, 4.6)]
    [InlineData(3990, 100000, 4.0)]
    [InlineData(100, 0, 0)]
    public void Price_per_message_matches_the_price_list(decimal price, int messages, decimal expectedAgorot) =>
        Assert.Equal(expectedAgorot, PlatformBilling.PricePerMessageAgorot(price, messages));

    [Fact]
    public void Vat_defaults_to_18_and_rejects_nonsense()
    {
        Assert.Equal(18m, PlatformBilling.VatPercent(Config()));
        Assert.Equal(17m, PlatformBilling.VatPercent(Config(("Billing:VatPercent", "17"))));
        Assert.Equal(18m, PlatformBilling.VatPercent(Config(("Billing:VatPercent", "abc"))));
        Assert.Equal(18m, PlatformBilling.VatPercent(Config(("Billing:VatPercent", "99"))));
    }

    [Fact]
    public void Package_name_falls_back_to_the_message_count()
    {
        Assert.Equal("2,500 הודעות", PlatformBilling.PackageDisplayName(null, 2500));
        Assert.Equal("חבילת פתיחה", PlatformBilling.PackageDisplayName(" חבילת פתיחה ", 2500));
    }

    [Fact]
    public void Terminal_requires_number_and_api_name()
    {
        Assert.Null(PlatformBilling.FromConfiguration(Config()));
        Assert.Null(PlatformBilling.FromConfiguration(Config(("Billing:Cardcom:TerminalNumber", "1000"))));
        Assert.Null(PlatformBilling.FromConfiguration(Config(("Billing:Cardcom:ApiName", "giorgio"))));

        var creds = PlatformBilling.FromConfiguration(Config(
            ("Billing:Cardcom:TerminalNumber", "1000"),
            ("Billing:Cardcom:ApiName", " giorgio "),
            ("Billing:Cardcom:ApiPassword", "secret"),
            ("Billing:Cardcom:MaxInstallments", "36")));
        Assert.NotNull(creds);
        Assert.Equal(1000, creds!.TerminalNumber);
        Assert.Equal("giorgio", creds.ApiName);
        Assert.Equal("secret", creds.ApiPassword);
        Assert.Equal(12, creds.MaxInstallments); // capped
        Assert.False(creds.SaveCardEnabled);
        Assert.Equal(CardcomDocumentBuilder.DefaultDocumentType, creds.DocumentTypeToCreate);
        Assert.Equal(0, creds.SiteId);
    }
}
