using George.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static George.Common.HttpHelper;

namespace George.Providers
{
    /// <summary>
    /// API documentation: https://apidoc.inforu.co.il/
    /// </summary>
	internal class InforuProvider
    {
        //***********************  Data members/Constants  ***********************//
        protected readonly ILogger<InforuProvider> _logger;
        private const string DEFAULT_CAMPAIGN_NAME = "Easy Life campaign";
        private static string _apiBaseUrl = string.Empty;   // e.g. https://api.inforu.co.il/
        private static string _apiToken = string.Empty;      // Basic token (same as IVR in your code)
        private static string _sender = string.Empty;        // Sender name/phone as supported by your Inforu account
        private static string? _campaignName;                // Optional; some accounts support it for SMS too
        protected readonly HttpHelper _httpHelper;


        //**************************    Construction    **************************//
        public InforuProvider(ILogger<InforuProvider> logger, HttpHelper httpHelper)
        {
            _logger = logger;
            _httpHelper = httpHelper;
        }

        //*************************    Properties    *************************//
        public static bool IsInitialized { get; private set; }



        //*************************    Public Methods    *************************//
        public static void Init(string apiBaseUrl, string apiToken, string sender, string? campaignName = null)
        {
            _apiBaseUrl = apiBaseUrl?.TrimEnd('/') + "/";
            _apiToken = apiToken;
            _campaignName = campaignName;
            _sender = sender ?? string.Empty;
            if (!_campaignName.HasValue())
                _campaignName += DEFAULT_CAMPAIGN_NAME;

            if (_apiBaseUrl.HasValue() && _apiToken.HasValue())
                IsInitialized = true;
            else
                IsInitialized = false;
        }

        /// <summary>Send with explicit per-account credentials (no static init required). Basic auth = base64(username:apiToken).</summary>
        public async Task<SmsSendResult> SendSmsAsync(string message, List<string> phoneNumbers, string apiBaseUrl, string username, string apiToken, string sender, SmsSendOptions? options = null, CancellationToken cancelToken = default)
        {
            if (!phoneNumbers.HasValue())
            {
                _logger.LogWarning("SMS phone list is empty.");
                return SmsSendResult.Fail("empty phone list");
            }

            var url = apiBaseUrl.TrimEnd('/') + "/SMS/SendSms";

            var req = new InforuSmsRequest
            {
                Data = new InforuSmsRequest.DataBlock
                {
                    Message = message,
                    Recipients = phoneNumbers.Select(p => new InforuSmsRequest.Recipient { Phone = p, CustomerMessageId = options?.CustomerMessageId }).ToList(),
                    Settings = new InforuSmsRequest.SettingsBlock
                    {
                        Sender = sender,
                        DeliveryNotificationUrl = options?.DeliveryNotificationUrl,
                    }
                }
            };

            var basicToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
            _httpHelper.SetHttpHeaderKey("Authorization", $"Basic {basicToken}");

            var res = await _httpHelper.HttpPostAsync<InforuSmsRequest, InforuSmsResponse>(req, url, cancelToken);
            if (!res.IsSuccessful)
            {
                _logger.LogError($"Inforu SMS send failed. HTTP: {res.HttpResponse}, Body: {res.HttpContent}");
                var code = res.HttpResponse != null ? (int)res.HttpResponse.StatusCode : 0;
                return SmsSendResult.Fail($"HTTP {code}");
            }

            // Inforu API v2 returns StatusId 1 on success (older shapes use Status). Missing status info = HTTP success is enough.
            var body = res.Data;
            if (body != null && body.StatusId.HasValue && body.StatusId.Value != 1)
            {
                var description = body.DetailedDescription.HasValue() ? $"{body.StatusDescription} - {body.DetailedDescription}" : (body.StatusDescription ?? body.Message);
                _logger.LogError($"Inforu SMS rejected. StatusId: {body.StatusId}, Description: {description}");
                return SmsSendResult.Fail($"Inforu {body.StatusId}: {description}", body.StatusId);
            }

            return SmsSendResult.Ok();
        }

        /// <summary>Admin/GetQuota. Empty <paramref name="level"/> = the authenticated user's own customer - the way a sub-account reads its own balance.</summary>
        public async Task<InforuQuotaInfo> GetQuotaAsync(string apiBaseUrl, string username, string apiToken, string? level, string? levelValue, CancellationToken cancelToken = default)
        {
            var url = apiBaseUrl.TrimEnd('/') + "/Admin/GetQuota";
            var req = new InforuQuotaRequest { Data = new InforuQuotaRequest.DataBlock { QuotaUsageType = "SMS", Level = level ?? string.Empty, LevelValue = levelValue ?? string.Empty } };
            SetBasicAuth(username, apiToken);

            var res = await _httpHelper.HttpPostAsync<InforuQuotaRequest, InforuQuotaResponse>(req, url, cancelToken);
            if (!res.IsSuccessful)
                return new InforuQuotaInfo { Error = $"HTTP {(res.HttpResponse != null ? (int)res.HttpResponse.StatusCode : 0)}" };
            var body = res.Data;
            if (body == null || body.StatusId != 1)
                return new InforuQuotaInfo { Error = Describe(body?.StatusId, body?.StatusDescription, body?.DetailedDescription) };

            var sms = body.Data?.List?.FirstOrDefault(i => string.Equals(i.QuotaUsageType, "SMS", StringComparison.OrdinalIgnoreCase)) ?? body.Data?.List?.FirstOrDefault();
            return new InforuQuotaInfo
            {
                Success = true,
                Level = body.Data?.Level,
                LevelValue = body.Data?.LevelValue?.ToString(),
                RemainingSms = sms?.RemainingQuota is long r ? (int)Math.Clamp(r, int.MinValue, int.MaxValue) : null,
                QuotaType = sms?.QuotaType,
                WarningLevel = sms?.WarningLevel is long w ? (int)Math.Clamp(w, int.MinValue, int.MaxValue) : null,
            };
        }

        /// <summary>Admin/CreateOrAddQuota with the PARENT credentials: adds <paramref name="amount"/> SMS to a Packages quota. Never switches the quota type.</summary>
        public async Task<InforuAddQuotaOutcome> AddQuotaAsync(string apiBaseUrl, string username, string apiToken, string level, string levelValue, int amount, CancellationToken cancelToken = default)
        {
            var url = apiBaseUrl.TrimEnd('/') + "/Admin/CreateOrAddQuota";
            var req = new InforuAddQuotaRequest { Data = new InforuAddQuotaRequest.DataBlock { Level = level, LevelValue = levelValue, QuotaAmount = amount.ToString() } };
            SetBasicAuth(username, apiToken);

            var res = await _httpHelper.HttpPostAsync<InforuAddQuotaRequest, InforuAddQuotaResponse>(req, url, cancelToken);
            if (!res.IsSuccessful)
                return new InforuAddQuotaOutcome { Error = $"HTTP {(res.HttpResponse != null ? (int)res.HttpResponse.StatusCode : 0)}" };
            var body = res.Data;
            if (body == null || body.StatusId != 1)
                return new InforuAddQuotaOutcome { Error = Describe(body?.StatusId, body?.StatusDescription, body?.DetailedDescription) };
            return new InforuAddQuotaOutcome
            {
                Success = true,
                RemainingBefore = body.Data?.Before?.RemainingQuota is long b ? (int)b : null,
                RemainingAfter = body.Data?.After?.RemainingQuota is long a ? (int)a : null,
            };
        }

        private void SetBasicAuth(string username, string apiToken)
        {
            var basicToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
            _httpHelper.SetHttpHeaderKey("Authorization", $"Basic {basicToken}");
        }

        private static string Describe(int? statusId, string? description, string? detailed) =>
            $"Inforu {statusId?.ToString() ?? "?"}: {description}{(detailed.HasValue() ? " - " + detailed : string.Empty)}";

        public async Task<bool> SendSmsAsync(string message, List<string> phoneNumbers, CancellationToken cancelToken = default)
        {
            VerifyInit();

            if (!phoneNumbers.HasValue())
            {
                _logger.LogWarning("SMS phone list is empty.");
                return false;
            }

            var url = _apiBaseUrl + "SMS/SendSms"; // typical route; adjust if your tenant differs

            var req = new InforuSmsRequest
            {
                Data = new InforuSmsRequest.DataBlock
                {
                    Message = message,
                    Recipients = new List<InforuSmsRequest.Recipient>(),
                    Settings = new InforuSmsRequest.SettingsBlock
                    {
                        Sender = _sender
                    }
                }
            };

            foreach (var phone in phoneNumbers)
                req.Data.Recipients.Add(new InforuSmsRequest.Recipient { Phone = phone });

            // Send it.
            var res = await ExecuteAsync(url, req);
            if (!res.IsSuccessful)
                _logger.LogError($"Inforu SMS send failed. HTTP: {res.HttpResponse}, Body: {res.HttpContent}");

            return res.IsSuccessful;
        }

        // If you receive Inforu SMS callbacks as form posts, you can keep the same shape as 019:
        public SmsUserResponse? ParseUserResponse(IFormCollection response)
        {
            // Many SMS providers post back different keys; reuse your 019 parsing if your webhook is aligned
            var dict = response.ToDictionary(k => k.Key, v => v.Value.ToString());

            var res = new SmsUserResponse();
            if (dict.TryGetValue("message", out var msg)) res.Value = msg;
            if (dict.TryGetValue("phone", out var phone)) res.Phone = phone;
            if (dict.TryGetValue("date", out var date)) res.Date = date;

            if (res.Value.HasValue() && res.Phone.HasValue()) return res;
            return null;
        }

        //*************************    Private Methods    ************************//
        private void VerifyInit()
        {
            if (!IsInitialized)
                throw new GeorgeNotInitializedException("InforuSmsProvider is not initialized");
        }

        private bool IsOk(InforuSmsResponse resp)
        {
            // Mirror the simple 019 check. Adjust once you see the exact Inforu payload.
            // Many Inforu responses include Status="1" or Status="OK"/"Success".
            return resp.Status.EqualsCI("0") == false; // Example: treat non-"0" 019 style as success; adjust to your contract
        }

        private async Task<HttpHelperResult<InforuSmsResponse>> ExecuteAsync(string url, InforuSmsRequest req, CancellationToken cancelToken = default)
        {
            // Set authentication.
            _httpHelper.SetHttpHeaderKey("Authorization", $"Basic {_apiToken}");
            //_httpHelper.SetHttpHeaderKey("Content-Type", "application/json");

            // Sens the request to active trail API.
            return await _httpHelper.HttpPostAsync<InforuSmsRequest, InforuSmsResponse>(req, url, cancelToken);
        }

    }
}
