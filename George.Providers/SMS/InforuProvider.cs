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
