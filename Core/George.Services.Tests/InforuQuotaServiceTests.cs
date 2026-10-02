using George.Services.Marketing;
using System.Net;
using System.Text;
using George.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

/// <summary>The two Inforu admin calls against a fake HTTP layer that answers with the documented bodies (apidoc.inforu.co.il → General).</summary>
public class InforuQuotaProviderTests
{
    private sealed class FakeInforu : HttpMessageHandler
    {
        public List<(string Url, string Auth, string Body)> Requests { get; } = new();
        public Func<string, string> Answer { get; set; } = _ => "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString() ?? string.Empty, body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Answer(request.RequestUri!.AbsolutePath), Encoding.UTF8, "application/json") };
        }
    }

    private static (SmsProvider Provider, FakeInforu Http) NewProvider()
    {
        var http = new FakeInforu();
        var provider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new George.Common.HttpHelper(new HttpClient(http)));
        return (provider, http);
    }

    private static SmsAccountConfig SubAccount() => new()
    {
        Provider = SmsProviderNames.Inforu, Username = "shop1", ApiToken = "tok-1", FromName = "Giorgio", BilledByPlatform = true,
    };

    [Fact]
    public async Task GetQuota_reads_own_customer_with_sub_account_credentials()
    {
        var (provider, http) = NewProvider();
        http.Answer = _ => @"{""StatusId"":1,""StatusDescription"":""Success"",""Data"":{""Level"":""Customer"",""LevelValue"":26745,""List"":[
            {""QuotaUsageType"":""Newsletters"",""QuotaType"":""Packages"",""RemainingQuota"":100,""WarningLevel"":20},
            {""QuotaUsageType"":""SMS"",""QuotaType"":""Packages"",""RemainingQuota"":1996,""WarningLevel"":200}]}}";

        var info = await provider.GetInforuQuotaAsync(SubAccount());

        Assert.True(info.Success);
        Assert.Equal(("Customer", "26745", 1996, "Packages", 200), (info.Level, info.LevelValue, info.RemainingSms, info.QuotaType, info.WarningLevel));
        var req = Assert.Single(http.Requests);
        Assert.Equal("https://capi.inforu.co.il/api/v2/Admin/GetQuota", req.Url);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("shop1:tok-1")), req.Auth);
        Assert.Contains("\"Level\":\"\"", req.Body);          // empty level = my own customer
        Assert.Contains("\"QuotaUsageType\":\"SMS\"", req.Body);
    }

    [Fact]
    public async Task GetQuota_surfaces_provider_rejection()
    {
        var (provider, http) = NewProvider();
        http.Answer = _ => @"{""StatusId"":-2,""StatusDescription"":""Bad user name or password"",""DetailedDescription"":""""}";
        var info = await provider.GetInforuQuotaAsync(SubAccount());
        Assert.False(info.Success);
        Assert.Contains("-2", info.Error);
        Assert.Contains("Bad user name", info.Error);
    }

    [Fact]
    public async Task AddQuota_uses_parent_credentials_customer_level_and_packages()
    {
        var (provider, http) = NewProvider();
        http.Answer = _ => @"{""StatusId"":1,""StatusDescription"":""Success"",""Data"":{""Level"":""Customer"",""LevelValue"":26745,
            ""Before"":{""QuotaUsageType"":""SMS"",""QuotaType"":""Packages"",""QuotaAmount"":100,""RemainingQuota"":100},
            ""After"":{""QuotaUsageType"":""SMS"",""QuotaType"":""Packages"",""QuotaAmount"":5100,""RemainingQuota"":5100}}}";
        var parent = new InforuParentCredentials { Username = "giorgio-parent", ApiToken = "parent-tok" };

        var outcome = await provider.AddInforuQuotaAsync(parent, "Customer", "26745", 5000);

        Assert.True(outcome.Success);
        Assert.Equal((100, 5100), (outcome.RemainingBefore, outcome.RemainingAfter));
        var req = Assert.Single(http.Requests);
        Assert.Equal("https://capi.inforu.co.il/api/v2/Admin/CreateOrAddQuota", req.Url);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("giorgio-parent:parent-tok")), req.Auth);
        Assert.Contains("\"Level\":\"Customer\"", req.Body);
        Assert.Contains("\"LevelValue\":\"26745\"", req.Body);
        Assert.Contains("\"QuotaType\":\"Packages\"", req.Body);
        Assert.Contains("\"QuotaAmount\":\"5000\"", req.Body);
        // Never flip a Monthly quota into Packages behind the customer's back.
        Assert.Contains("\"AllowChangeQuotaType\":\"false\"", req.Body);
    }
}

public class InforuQuotaServiceConfigTests
{
    [Fact]
    public async Task AddQuota_refuses_to_top_up_a_monthly_renewal_sub_account()
    {
        // QA finding 2026-10-01: the Inforu sub-account quota type was "Monthly renewal". CreateOrAddQuota only ADDS to
        // "Packages" - on a monthly quota it OVERWRITES the allowance, so the service must stop before calling it.
        var http = new FakeInforu();
        http.Answer = path => path.EndsWith("/GetQuota")
            ? @"{""StatusId"":1,""Data"":{""Level"":""Customer"",""LevelValue"":1077857,""List"":[{""QuotaUsageType"":""SMS"",""QuotaType"":""Monthly renewal"",""QuotaAmount"":100,""RemainingQuota"":94}]}}"
            : @"{""StatusId"":1,""Data"":{}}";
        var provider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new George.Common.HttpHelper(new HttpClient(http)));
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sms:Inforu:ParentUsername"] = "giorgio", ["Sms:Inforu:ParentApiToken"] = "t",
        }).Build();
        var svc = new InforuQuotaService(provider, null!, cfg, NullLogger<InforuQuotaService>.Instance);
        var sub = new SmsAccountConfig { Provider = SmsProviderNames.Inforu, Username = "shop1", ApiToken = "tok-1", FromName = "Giorgio", BilledByPlatform = true, InforuCustomerId = "1077857" };

        var (error, remaining) = await svc.AddAsync(18, sub, 500, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("Monthly renewal", error);
        Assert.Null(remaining);
        Assert.All(http.Requests, r => Assert.DoesNotContain("CreateOrAddQuota", r.Url));
    }

    private sealed class FakeInforu : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = new();
        public Func<string, string> Answer { get; set; } = _ => "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Answer(request.RequestUri!.AbsolutePath), Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public void Parent_credentials_come_from_configuration()
    {
        var provider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new George.Common.HttpHelper(new HttpClient()));
        var none = new InforuQuotaService(provider, null!, new ConfigurationBuilder().Build(), NullLogger<InforuQuotaService>.Instance);
        Assert.Null(none.ParentCredentials);

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sms:Inforu:ParentUsername"] = " giorgio ", ["Sms:Inforu:ParentApiToken"] = "t",
        }).Build();
        var some = new InforuQuotaService(provider, null!, cfg, NullLogger<InforuQuotaService>.Instance);
        Assert.Equal(("giorgio", "t", null), (some.ParentCredentials!.Username, some.ParentCredentials.ApiToken, some.ParentCredentials.ApiBaseUrl));
    }
}
