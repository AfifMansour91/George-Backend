using George.Common;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// <see cref="FileHelper.UpgradeInsecureExternalUrl"/> — public file links must come out https even
/// when the configured storage base path says "http://" (Dubi-Dagim kiosk: home video stored with an
/// http url → mixed-content HTTPS warning on the kiosk).
/// </summary>
public class FileHelperExternalUrlTests
{
    [Theory]
    [InlineData("http://api.storeos.co.il/files/PROD/Temp/video.mp4",
                "https://api.storeos.co.il/files/PROD/Temp/video.mp4")]
    [InlineData("HTTP://api.storeos.co.il/files/x.png",
                "https://api.storeos.co.il/files/x.png")]
    [InlineData("http://qa-api.m-dev.com/files/x.png",
                "https://qa-api.m-dev.com/files/x.png")]
    public void Http_on_public_host_is_upgraded(string input, string expected)
    {
        Assert.Equal(expected, FileHelper.UpgradeInsecureExternalUrl(input));
    }

    [Theory]
    [InlineData("http://localhost:44378/files/x.png")]
    [InlineData("http://127.0.0.1/files/x.png")]
    public void Loopback_host_stays_http(string input)
    {
        Assert.Equal(input, FileHelper.UpgradeInsecureExternalUrl(input));
    }

    [Theory]
    [InlineData("https://api.storeos.co.il/files/x.png")]
    [InlineData("https://teragon.s3.eu-central-1.amazonaws.com/Temp/x.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void Https_or_empty_is_unchanged(string? input)
    {
        Assert.Equal(input ?? string.Empty, FileHelper.UpgradeInsecureExternalUrl(input));
    }
}
