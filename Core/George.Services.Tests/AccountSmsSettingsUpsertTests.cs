using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Providers;
using George.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Who may mark an SMS row as a platform sub-account. Regression for QA 1.10: the controller never handed the
/// caller to AccountSmsService, so a super-admin's BilledByPlatform=true was silently dropped and the account
/// showed up as "own provider" (exempt from metering).
/// </summary>
public class AccountSmsSettingsUpsertTests
{
    private static GeorgeDBContext NewContext()
    {
        var options = new DbContextOptionsBuilder<GeorgeDBContextBase>()
            .UseInMemoryDatabase($"sms-upsert-{Guid.NewGuid()}")
            .EnableServiceProviderCaching(false)
            .Options;
        return new GeorgeDBContext(options);
    }

    private static async Task<(AccountSmsService Service, GeorgeDBContext Db, int AccountId)> ArrangeAsync(int callerRole, bool isMaster = false)
    {
        var db = NewContext();
        var account = new Account { Name = "acc", ManagerEmail = "m@x", Status = "Active", IsActive = true };
        db.Account.Add(account);
        var caller = new User { FirstName = "a", LastName = "b", FullName = "a b", RoleId = callerRole, AccountId = account.Id, Email = "c@x", Phone = "0500000000" };
        db.User.Add(caller);
        await db.SaveChangesAsync();

        var smsProvider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new HttpHelper(new HttpClient()));
        var service = new AccountSmsService(
            NullLogger<AccountSmsService>.Instance, null!, new CacheManager(),
            new AccountStorage(db, NullLogger<AccountStorage>.Instance), smsProvider,
            messageLogQueue: null, userStorage: new UserStorage(db, NullLogger<UserStorage>.Instance))
        {
            AuthUser = new AuthenticatedUser { Id = caller.Id, IsMaster = isMaster },
        };
        return (service, db, account.Id);
    }

    private static AccountSmsSettingsReq InforuSubAccountReq() => new()
    {
        IsEnabled = true, Provider = "Inforu", Username = "shop1", ApiToken = "tok", FromName = "Giorgio",
        BilledByPlatform = true, InforuCustomerId = "26745",
    };

    [Fact]
    public async Task System_admin_can_mark_a_platform_sub_account()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.Admin);
        var res = await service.UpsertSettingsAsync(accountId, InforuSubAccountReq(), default);
        Assert.True(res.IsSuccessful, res.Description);
        Assert.True(res.Data!.BilledByPlatform);
        Assert.Equal("26745", res.Data.InforuCustomerId);
        Assert.False(res.Data.UsingSystemDefault);
    }

    [Fact]
    public async Task Master_can_mark_a_platform_sub_account()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.AccountAdmin, isMaster: true);
        var res = await service.UpsertSettingsAsync(accountId, InforuSubAccountReq(), default);
        Assert.True(res.Data!.BilledByPlatform);
    }

    [Fact]
    public async Task Account_admin_cannot_bring_its_own_inforu()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.AccountAdmin);
        var res = await service.UpsertSettingsAsync(accountId, InforuSubAccountReq(), default);
        Assert.False(res.IsSuccessful);
        Assert.Equal(AccountSmsService.ErrInforuPlatformOnly, res.Description);
    }

    [Fact]
    public async Task Account_admin_can_save_its_own_activetrail_but_not_billing()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.AccountAdmin);
        var res = await service.UpsertSettingsAsync(accountId, new AccountSmsSettingsReq
        {
            IsEnabled = true, Provider = "ActiveTrail", ApiToken = "0Xtok", FromName = "My Shop", BilledByPlatform = true,
        }, default);
        Assert.True(res.IsSuccessful, res.Description);
        Assert.False(res.Data!.BilledByPlatform);      // silently kept at its stored value (false)
        Assert.Equal("My Shop", res.Data.FromName);
    }

    [Fact]
    public async Task Shop_keeps_its_own_activetrail_next_to_giorgios_inforu_and_picks_the_active_one()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.Admin);
        await service.UpsertSettingsAsync(accountId, InforuSubAccountReq(), default);

        // The shop (a restricted caller) adds its own ActiveTrail and makes it active - Giorgio's row stays untouched.
        service.AuthUser = new AuthenticatedUser { Id = 999, IsMaster = false };
        var own = await service.UpsertSettingsAsync(accountId, new AccountSmsSettingsReq
        {
            IsEnabled = true, Provider = "ActiveTrail", ApiToken = "0Xother", FromName = "My Shop",
        }, default);
        Assert.True(own.IsSuccessful, own.Description);
        Assert.Equal("ActiveTrail", own.Data!.ActiveProvider);
        Assert.Equal("My Shop", own.Data.FromName);              // flat fields = the active row
        Assert.NotNull(own.Data.Inforu);
        Assert.False(own.Data.Inforu!.IsEnabled);
        Assert.True(own.Data.Inforu.BilledByPlatform);
        Assert.Equal("Giorgio", own.Data.Inforu.FromName);
        Assert.True(own.Data.ActiveTrail!.IsEnabled);

        // It can switch back to Giorgio's Inforu (choosing is allowed, editing is not)...
        var back = await service.SetActiveProviderAsync(accountId, new AccountSmsActiveProviderReq { Provider = "Inforu" }, default);
        Assert.True(back.IsSuccessful, back.Description);
        Assert.Equal("Inforu", back.Data!.ActiveProvider);
        Assert.True(back.Data.BilledByPlatform);
        Assert.False(back.Data.ActiveTrail!.IsEnabled);

        // ...but NOT to the system default - that is the platform's call.
        var none = await service.SetActiveProviderAsync(accountId, new AccountSmsActiveProviderReq { Provider = null }, default);
        Assert.False(none.IsSuccessful);
        Assert.Equal(AccountSmsService.ErrSystemAccountPlatformOnly, none.Description);
        service.AuthUser = new AuthenticatedUser { Id = 1, IsMaster = true };
        var asAdmin = await service.SetActiveProviderAsync(accountId, new AccountSmsActiveProviderReq { Provider = null }, default);
        Assert.Null(asAdmin.Data!.ActiveProvider);
        Assert.True(asAdmin.Data.UsingSystemDefault);
        service.AuthUser = new AuthenticatedUser { Id = 999, IsMaster = false };

        // But it can neither delete Giorgio's row nor pick a provider it has no credentials for.
        Assert.False((await service.DeleteSettingsAsync(accountId, "Inforu", default)).IsSuccessful);
        Assert.True((await service.DeleteSettingsAsync(accountId, "ActiveTrail", default)).IsSuccessful);
        Assert.False((await service.SetActiveProviderAsync(accountId, new AccountSmsActiveProviderReq { Provider = "ActiveTrail" }, default)).IsSuccessful);
        var final = await service.GetSettingsAsync(accountId, default);
        Assert.NotNull(final.Data!.Inforu);
        Assert.Null(final.Data.ActiveTrail);
    }

    [Fact]
    public async Task Inforu_sender_with_hebrew_or_spaces_is_rejected()
    {
        var (service, _, accountId) = await ArrangeAsync((int)UserRole.Admin);
        var req = InforuSubAccountReq();
        req.FromName = "מעדני גורמה";
        var res = await service.UpsertSettingsAsync(accountId, req, default);
        Assert.False(res.IsSuccessful);
        Assert.Contains("שם השולח", res.Description);
    }
}
