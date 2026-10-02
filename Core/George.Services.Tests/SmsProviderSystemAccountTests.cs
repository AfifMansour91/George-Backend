using System.Net;
using System.Text;
using George.Common;
using George.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// The SYSTEM SMS account (OTP + shops without their own row) can run on ActiveTrail or on Inforu (config Sms:Provider).
/// These tests touch SmsProvider's static state, so they run serialized and always restore ActiveTrail.
/// </summary>
[Collection("SmsProviderStatics")]
public class SmsProviderSystemAccountTests : IDisposable
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Url, string Auth, string Body)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString() ?? string.Empty, body));
            var answer = request.RequestUri!.AbsolutePath.EndsWith("/SendSms") ? @"{""StatusId"":1,""StatusDescription"":""Success""}" : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private readonly RecordingHandler _http = new();
    private readonly SmsProvider _provider;

    public SmsProviderSystemAccountTests()
    {
        SmsProvider.Init("https://activetrail.test/api", "at-token", "user", "0500000000", "campaign", "Giorgio");
        _provider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new George.Common.HttpHelper(new HttpClient(_http)));
    }

    public void Dispose()
    {
        // Back to the default so other test classes see the historical behaviour.
        SmsProvider.InitSystemProvider(null, null, null, null);
    }

    [Fact]
    public async Task Default_system_account_is_ActiveTrail()
    {
        Assert.Null(SmsProvider.InitSystemProvider(null, null, null, null));
        Assert.Equal(SmsProviderNames.ActiveTrail, SmsProvider.SystemProviderName);
        Assert.True(SmsProvider.CanSendWith(null));

        await _provider.SendTextAsync("0541234567", "hi", accountConfig: null);

        var req = Assert.Single(_http.Requests);
        Assert.StartsWith("https://activetrail.test/api", req.Url);
    }

    [Fact]
    public async Task System_account_on_Inforu_sends_OTP_and_operational_through_Inforu_with_system_credentials()
    {
        Assert.Null(SmsProvider.InitSystemProvider("Inforu", "giorgio-sys", "sys-token", "giorgio"));
        Assert.Equal(SmsProviderNames.Inforu, SmsProvider.SystemProviderName);
        Assert.True(SmsProvider.IsInitialized);
        Assert.True(SmsProvider.CanSendWith(null));

        var result = await _provider.SendTextDetailedAsync("0541234567", "hi", accountConfig: null,
            options: new SmsSendOptions { CustomerMessageId = "77", DeliveryNotificationUrl = "https://g.test/Webhooks/Sms/Inforu?s=x" });
        await _provider.SendOtpMessageAsync("0541234567", 1, "123456");

        Assert.True(result.Success);
        Assert.Equal(2, _http.Requests.Count);
        Assert.All(_http.Requests, r =>
        {
            Assert.Equal("https://capi.inforu.co.il/api/v2/SMS/SendSms", r.Url);
            Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("giorgio-sys:sys-token")), r.Auth);
            Assert.Contains("\"Sender\":\"giorgio\"", r.Body);
        });
        // Per-message options (DLR) ride along on the system account too.
        Assert.Contains("\"CustomerMessageID\":\"77\"", _http.Requests[0].Body);
        Assert.Contains("DeliveryNotificationUrl", _http.Requests[0].Body);
        Assert.Contains("123456", _http.Requests[1].Body);
    }

    [Fact]
    public async Task Shop_with_its_own_ActiveTrail_row_is_not_moved_when_the_system_switches_to_Inforu()
    {
        SmsProvider.InitSystemProvider("Inforu", "giorgio-sys", "sys-token", "giorgio");
        var own = new SmsAccountConfig { Provider = SmsProviderNames.ActiveTrail, ApiToken = "shop-token", FromName = "Shop", ApiBaseUrl = "https://activetrail.test/shop" };

        await _provider.SendTextAsync("0541234567", "hi", own);

        var req = Assert.Single(_http.Requests);
        Assert.StartsWith("https://activetrail.test/shop", req.Url);
    }

    [Fact]
    public async Task Inforu_chosen_without_credentials_is_reported_and_refuses_system_sends()
    {
        var problem = SmsProvider.InitSystemProvider("Inforu", "giorgio-sys", null, "giorgio");

        Assert.NotNull(problem);
        Assert.False(SmsProvider.IsInitialized);
        Assert.False(SmsProvider.CanSendWith(null));
        await Assert.ThrowsAsync<GeorgeNotInitializedException>(() => _provider.SendTextAsync("0541234567", "hi", accountConfig: null));
        Assert.Empty(_http.Requests);

        // A shop's own valid row still works - the system account's problem is not theirs.
        var own = new SmsAccountConfig { Provider = SmsProviderNames.Inforu, Username = "shop1", ApiToken = "t", FromName = "Shop", BilledByPlatform = true };
        Assert.True(SmsProvider.CanSendWith(own));
    }

}
