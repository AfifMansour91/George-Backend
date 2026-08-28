using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

public class AccountSmsServiceTests
{
    private static AccountSmsSettings ValidSettings() => new()
    {
        AccountId = 1,
        IsEnabled = true,
        Provider = "ActiveTrail",
        ApiToken = "0Xtoken1234",
        FromName = "MyShop",
    };

    [Fact]
    public void MapToConfig_valid_enabled_row_returns_config()
    {
        var config = AccountSmsService.MapToConfig(ValidSettings());
        Assert.NotNull(config);
        Assert.Equal("0Xtoken1234", config!.ApiToken);
        Assert.Equal("MyShop", config.FromName);
        Assert.Null(config.ApiBaseUrl);
    }

    [Fact]
    public void MapToConfig_null_row_returns_null()
    {
        Assert.Null(AccountSmsService.MapToConfig(null));
    }

    [Fact]
    public void MapToConfig_disabled_row_returns_null()
    {
        var settings = ValidSettings();
        settings.IsEnabled = false;
        Assert.Null(AccountSmsService.MapToConfig(settings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapToConfig_missing_token_returns_null(string? token)
    {
        var settings = ValidSettings();
        settings.ApiToken = token;
        Assert.Null(AccountSmsService.MapToConfig(settings));
    }

    [Fact]
    public void MapToConfig_missing_from_name_returns_null()
    {
        var settings = ValidSettings();
        settings.FromName = " ";
        Assert.Null(AccountSmsService.MapToConfig(settings));
    }

    [Fact]
    public void MapToConfig_unknown_provider_returns_null()
    {
        var settings = ValidSettings();
        settings.Provider = "Inforu";
        Assert.Null(AccountSmsService.MapToConfig(settings));
    }

    [Fact]
    public void MapToConfig_trims_and_keeps_url_override()
    {
        var settings = ValidSettings();
        settings.ApiBaseUrl = " https://api.example.com/sms ";
        var config = AccountSmsService.MapToConfig(settings);
        Assert.Equal("https://api.example.com/sms", config!.ApiBaseUrl);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("abc", "••••")]
    [InlineData("abcd", "••••")]
    [InlineData("0X614FC42D", "••••C42D")]
    public void MaskSecret_masks_all_but_last_four(string? secret, string? expected)
    {
        Assert.Equal(expected, AccountSmsService.MaskSecret(secret));
    }
}
