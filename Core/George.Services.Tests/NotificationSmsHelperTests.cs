using George.Services;
using Xunit;

namespace George.Services.Tests;

public class NotificationSmsHelperTests
{
    [Fact]
    public void ParseRecipientPhones_splits_comma_and_semicolon_lists()
    {
        var phones = NotificationSmsHelper.ParseRecipientPhones("050-1111111, 0522222222;0533333333");
        Assert.Equal(3, phones.Count);
        Assert.Equal("050-1111111", phones[0]);
        Assert.Equal("0522222222", phones[1]);
        Assert.Equal("0533333333", phones[2]);
    }

    [Theory]
    [InlineData("sms", true)]
    [InlineData("SMS", true)]
    [InlineData("whatsapp", false)]
    [InlineData(null, false)]
    public void IsSmsChannel_matches_expected(string? channel, bool expected)
    {
        Assert.Equal(expected, NotificationSmsHelper.IsSmsChannel(channel));
    }
}
