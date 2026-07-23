using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

public class NotificationSettingsResolverTests
{
    private static AccountNotificationSettings Row(int? siteId, string? phones = null, bool isDeleted = false) =>
        new() { AccountId = 1, SiteId = siteId, NewOrderManagerPhoneNumbers = phones, IsDeleted = isDeleted };

    [Fact]
    public void Resolve_SiteOverrideWins_OverAccountDefault()
    {
        var rows = new[] { Row(null, "050-1"), Row(7, "050-2") };
        var resolved = NotificationSettingsResolver.Resolve(rows, 7);
        Assert.Equal("050-2", resolved?.NewOrderManagerPhoneNumbers);
    }

    [Fact]
    public void Resolve_NoOverride_FallsBackToAccountDefault()
    {
        var rows = new[] { Row(null, "050-1"), Row(7, "050-2") };
        var resolved = NotificationSettingsResolver.Resolve(rows, 8);
        Assert.Equal("050-1", resolved?.NewOrderManagerPhoneNumbers);
    }

    [Fact]
    public void Resolve_NullSiteId_ReturnsAccountDefault_NeverAnOverride()
    {
        var rows = new[] { Row(7, "050-2"), Row(null, "050-1") };
        var resolved = NotificationSettingsResolver.Resolve(rows, null);
        Assert.Equal("050-1", resolved?.NewOrderManagerPhoneNumbers);
    }

    [Fact]
    public void Resolve_DeletedRowsAreIgnored()
    {
        var rows = new[] { Row(null, "050-1"), Row(7, "050-2", isDeleted: true) };
        var resolved = NotificationSettingsResolver.Resolve(rows, 7);
        Assert.Equal("050-1", resolved?.NewOrderManagerPhoneNumbers);
    }

    [Fact]
    public void Resolve_NoRows_ReturnsNull()
    {
        Assert.Null(NotificationSettingsResolver.Resolve((IEnumerable<AccountNotificationSettings>?)null, 7));
        Assert.Null(NotificationSettingsResolver.Resolve(Array.Empty<AccountNotificationSettings>(), 7));
    }

    [Fact]
    public void Resolve_OnlyOverridesExist_OtherSiteGetsNothing()
    {
        // No account default row: a site without its own override must get null, not another site's row.
        var rows = new[] { Row(7, "050-2") };
        Assert.Null(NotificationSettingsResolver.Resolve(rows, 8));
    }

    [Fact]
    public void HasSiteOverride_TrueOnlyForExactSiteRow()
    {
        var rows = new[] { Row(null, "050-1"), Row(7, "050-2") };
        Assert.True(NotificationSettingsResolver.HasSiteOverride(rows, 7));
        Assert.False(NotificationSettingsResolver.HasSiteOverride(rows, 8));
    }
}
