using George.Common;
using George.Providers.Sms019;
using George.Common;
using George.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using George.Providers.ActiveTrail;

namespace George.Providers
{
    public class SmsProvider
    {
        //***********************  Data members/Constants  ***********************//
        private const string CAMPAIGN_NAME = "Easy Life campaign";
        private static string _apiBaseUrl = string.Empty;
        private static string _campaignUrl = string.Empty;
        private static string _authToken = string.Empty;
        private static string _username = string.Empty;
        private static string _sourcePhone = string.Empty;
        private static string _displayName = string.Empty;
        /// <summary>Host only (e.g. app.example.com), no scheme. Last line of login OTP SMS becomes <c>@host #code</c> for Chrome Web OTP - must match the site origin.</summary>
        private static string _otpWebOriginHost = string.Empty;
        /// <summary>Which provider the SYSTEM account (OTP + shops without their own row) sends through. Config <c>Sms:Provider</c>; default ActiveTrail.</summary>
        private static string _systemProviderName = SmsProviderNames.ActiveTrail;
        /// <summary>System Inforu credentials (<c>Sms:Inforu:Username/ApiToken/Sender</c>) - used only when <see cref="_systemProviderName"/> is Inforu.</summary>
        private static SmsAccountConfig? _systemInforu;
        protected readonly ILogger<SmsProvider> _logger;
        protected readonly HttpHelper _httpHelper;

        /// <summary>Inforu API v2 base URL (per-account Inforu configs use it unless they set their own ApiBaseUrl).</summary>
        private const string INFORU_DEFAULT_API_BASE_URL = "https://capi.inforu.co.il/api/v2";

        //private Sms019Provider _provider;
        private ActiveTrailSmsProvider _provider;
        private InforuProvider _inforuProvider;
        //private static Dictionary<string, string> _messageTemplates = new();


        //**************************    Construction    **************************//
        public SmsProvider(ILoggerFactory loggerFactory, ILogger<SmsProvider> logger, HttpHelper httpHelper)
        {
            _logger = logger;
            _httpHelper = httpHelper;
            _provider = new ActiveTrailSmsProvider(httpHelper);
            _inforuProvider = new InforuProvider(loggerFactory.CreateLogger<InforuProvider>(), httpHelper);
        }


        //*************************    Properties    *************************//

        public static bool IsInitialized
        {
            get
            {
                if (SystemIsInforu)
                    return _systemInforu?.IsValid == true;
                return ActiveTrailSmsProvider.IsInitialized;
            }
        }

        /// <summary>"ActiveTrail" or "Inforu" - the provider behind the system account right now.</summary>
        public static string SystemProviderName => _systemProviderName;

        /// <summary>Sender name recipients see on system-account sends (ActiveTrail DisplayName or the system Inforu sender).</summary>
        public static string SystemSenderName => SystemIsInforu ? (_systemInforu?.FromName ?? string.Empty) : _displayName;

        public static bool SystemIsInforu => string.Equals(_systemProviderName, SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase);

        /// <summary>The system Inforu account as a config (null unless the system provider is Inforu and fully configured). Lets callers read its quota like any sub-account.</summary>
        public static SmsAccountConfig? SystemInforuConfig => SystemIsInforu && _systemInforu?.IsValid == true ? _systemInforu : null;


        //*************************    Public Methods    *************************//

        public static void Init(string apiBaseUrl, string authToken, string username, string sourcePhone, string campaignUrl, string? displayName = null, string? otpWebOriginHost = null)
        {
            _apiBaseUrl = apiBaseUrl;
            _authToken = authToken;
            _username = username;
            _sourcePhone = sourcePhone;
            _campaignUrl = campaignUrl;
            _otpWebOriginHost = NormalizeOtpWebOriginHost(otpWebOriginHost);

            if (displayName != null)
                _displayName = displayName;

            ActiveTrailSmsProvider.Init(apiBaseUrl, campaignUrl, authToken, displayName);
        }

        /// <summary>
        /// Choose the system account's provider. ActiveTrail keeps using <see cref="Init"/>; Inforu needs the system Inforu user + token + sender.
        /// Returns null when the chosen provider is usable, else a human-readable problem (the caller logs it; sends then throw "not initialized").
        /// </summary>
        public static string? InitSystemProvider(string? providerName, string? inforuUsername, string? inforuApiToken, string? inforuSender, string? inforuApiBaseUrl = null)
        {
            var wantsInforu = string.Equals(providerName?.Trim(), SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase);
            _systemProviderName = wantsInforu ? SmsProviderNames.Inforu : SmsProviderNames.ActiveTrail;
            _systemInforu = new SmsAccountConfig
            {
                Provider = SmsProviderNames.Inforu,
                Username = inforuUsername?.Trim() ?? string.Empty,
                ApiToken = inforuApiToken?.Trim() ?? string.Empty,
                FromName = inforuSender?.Trim() ?? string.Empty,
                ApiBaseUrl = string.IsNullOrWhiteSpace(inforuApiBaseUrl) ? null : inforuApiBaseUrl.Trim(),
                BilledByPlatform = true,
            };
            if (!wantsInforu)
                return ActiveTrailSmsProvider.IsInitialized ? null : "Sms:Provider is ActiveTrail but the ActiveTrail system account is not configured (Sms:ActiveTrail:AuthToken missing).";
            if (!_systemInforu.IsValid)
                return "Sms:Provider is Inforu but Sms:Inforu:Username / ApiToken / Sender are not all set - system SMS (OTP, operational) will fail until they are.";
            return null;
        }

        /// <summary>The credentials a send will actually use: the account's own valid config, else the system Inforu account, else null (= system ActiveTrail).</summary>
        private static SmsAccountConfig? ResolveEffectiveConfig(SmsAccountConfig? accountConfig)
        {
            if (accountConfig?.IsValid == true)
                return accountConfig;
            return SystemInforuConfig;
        }

        //public static void SetTemplates(List<CommonMessageTemplate> messageTemplates)
        //{
        //    if (!messageTemplates.HasValue())
        //        return;

        //    foreach (var template in messageTemplates)
        //    {
        //        string key = GenerateTemplateKey(template.TypeId, template.LanguageId);

        //        _messageTemplates.AddOrUpdate(key, template.Text);
        //    }
        //}

        public async Task<bool> SendTextAsync(string phone, string text, CancellationToken cancelToken = default)
        {
            return await SendTextAsync(phone, text, accountConfig: null, cancelToken);
        }

        /// <summary>Send with per-account credentials; a null/invalid <paramref name="accountConfig"/> falls back to the system-wide SMS account.</summary>
        public async Task<bool> SendTextAsync(string phone, string text, SmsAccountConfig? accountConfig, CancellationToken cancelToken = default)
        {
            return (await SendTextDetailedAsync(phone, text, accountConfig, options: null, cancelToken)).Success;
        }

        /// <summary>Same as <see cref="SendTextAsync(string, string, SmsAccountConfig?, CancellationToken)"/> but returns the provider's verdict (error text, quota-exceeded) instead of a bare bool.</summary>
        public async Task<SmsSendResult> SendTextDetailedAsync(string phone, string text, SmsAccountConfig? accountConfig, SmsSendOptions? options, CancellationToken cancelToken = default)
        {
            VerifyInit(accountConfig);

            var phones = new List<string>() { phone };

            return await SendAsync(phones, text, accountConfig, options, cancelToken);
        }

        public async Task<bool> SendTextAsync(List<string> phones, string text, CancellationToken cancelToken = default)
        {
            VerifyInit();

            return (await SendAsync(phones, text, accountConfig: null, options: null, cancelToken)).Success;
        }

        /// <summary>Remaining SMS in the shop's own Inforu sub-account (read with ITS credentials; no level = its own customer).</summary>
        public Task<InforuQuotaInfo> GetInforuQuotaAsync(SmsAccountConfig config, CancellationToken cancelToken = default)
        {
            if (config?.IsValid != true || !config.IsInforu)
                return Task.FromResult(new InforuQuotaInfo { Error = "not an Inforu account" });
            var apiBaseUrl = config.ApiBaseUrl.HasValue() ? config.ApiBaseUrl! : INFORU_DEFAULT_API_BASE_URL;
            return _inforuProvider.GetQuotaAsync(apiBaseUrl, config.Username, config.ApiToken, level: null, levelValue: null, cancelToken);
        }

        /// <summary>Top up a sub-account's Inforu quota using the PLATFORM's parent credentials.</summary>
        public Task<InforuAddQuotaOutcome> AddInforuQuotaAsync(InforuParentCredentials parent, string level, string levelValue, int amount, CancellationToken cancelToken = default)
        {
            var apiBaseUrl = parent.ApiBaseUrl.HasValue() ? parent.ApiBaseUrl! : INFORU_DEFAULT_API_BASE_URL;
            return _inforuProvider.AddQuotaAsync(apiBaseUrl, parent.Username, parent.ApiToken, level, levelValue, amount, cancelToken);
        }

        /// <summary>True when a send can go out either via the given account config or via the system-wide account.</summary>
        public static bool CanSendWith(SmsAccountConfig? accountConfig)
        {
            return accountConfig?.IsValid == true || IsInitialized;
        }

        public async Task<bool> SendLoginMessageAsync(string phone, int languageId, string otp, CancellationToken cancelToken = default)
        {
            VerifyInit();

            _logger.LogTrace($"Sending login SMS to {phone}");

            // Build a the replacement tokens.
            Dictionary<string, string> tokens = new Dictionary<string, string>();
            tokens.Add("##OTP##", otp);

            return await SendAsync(phone, MessageType.Login, languageId, tokens, cancelToken);
        }

        public async Task<bool> SendValidatePhoneMessageAsync(string phone, int languageId, string otp, CancellationToken cancelToken = default)
        {
            VerifyInit();

            _logger.LogTrace($"Sending validate phone SMS to {phone}");

            // Build a the replacement tokens.
            Dictionary<string, string> tokens = new Dictionary<string, string>();
            tokens.Add("##OTP##", otp);

            return await SendAsync(phone, MessageType.ValidatePhone, languageId, tokens, cancelToken);
        }

        public async Task<bool> SendOtpMessageAsync(string phone, int languageId, string otp, CancellationToken cancelToken = default)
        {
            VerifyInit();

            _logger.LogTrace($"Sending OTP SMS to {phone}");

            // Human-readable line + standalone code line helps iOS/Android autofill. Optional last line @host #code for Chrome Web OTP (host must match page origin).
            var lines = new List<string>
            {
                $"קוד האימות שלך הוא: {otp}",
                otp
            };
            if (_otpWebOriginHost.Length > 0)
                lines.Add($"@{_otpWebOriginHost} #{otp}");
            string otpText = string.Join(Environment.NewLine, lines);

            var phones = new List<string>() { phone };
            return (await SendAsync(phones, otpText, accountConfig: null, options: null, cancelToken)).Success;
        }

        //*************************    Private Methods    ************************//

        private void VerifyInit(SmsAccountConfig? accountConfig = null)
        {
            // A valid per-account config carries its own credentials, so the system-wide init is not required for it.
            if (accountConfig?.IsValid == true)
                return;

            if (!IsInitialized)
                throw new GeorgeNotInitializedException("SMS provider is not initialized");
        }

        /// <summary>Strip scheme/path; return host only or empty if invalid.</summary>
        private static string NormalizeOtpWebOriginHost(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var s = value.Trim();
            if (s.StartsWith("@"))
                s = s.Substring(1).TrimStart();
            try
            {
                if (s.Contains("://", StringComparison.Ordinal))
                {
                    var uri = new Uri(s);
                    return uri.Host;
                }
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
            var cut = s.Split(new[] { '/', ' ', '?' }, StringSplitOptions.RemoveEmptyEntries);
            return cut.Length > 0 ? cut[0] : string.Empty;
        }

        private static string GenerateTemplateKey(MessageType typeId, int languageId)
        {
            return $"{(int)typeId}_{languageId}";
        }

        private string GetTemplate(MessageType typeId, int languageId)
        {
            string template = string.Empty;

            string key = GenerateTemplateKey(typeId, languageId);

            //if (_messageTemplates.TryGetValue(key, out template) == false)
            //    throw new GeorgeNotFoundException($"SMS message key template ({key}) was not found.");

            return template;
        }

        private async Task<bool> SendAsync(string phone, MessageType typeId, int languageId, Dictionary<string, string> tokens, CancellationToken cancelToken = default)
        {
            var phones = new List<string>() { phone };

            return await SendAsync(phones, typeId, languageId, tokens, cancelToken);
        }

        private async Task<bool> SendAsync(List<string> phones, MessageType typeId, int languageId, Dictionary<string, string> tokens, CancellationToken cancelToken = default)
        {
            // Get the message template.
            string text = GetTemplate(typeId, languageId);

            // Replace tokens.
            text = ConfigureMessage(text, tokens);

            _logger.LogTrace($"Sending SMS to {phones.Count} phones (first one is {phones[0]}).");

            // Same routing as every other system send (ActiveTrail or the system Inforu account).
            return (await SendAsync(phones, text, accountConfig: null, options: null, cancelToken)).Success;
        }

        private async Task<SmsSendResult> SendAsync(List<string> phones, string text, SmsAccountConfig? accountConfig, SmsSendOptions? options, CancellationToken cancelToken = default)
        {
            _logger.LogTrace($"Sending SMS to {phones.Count} phones (first one is {phones[0]}).");

            try
            {
                // The account's own Inforu row, or the system Inforu account (Sms:Provider=Inforu), sends through Inforu;
                // anything else goes through ActiveTrail (account row or the system ActiveTrail creds).
                var effective = ResolveEffectiveConfig(accountConfig);
                if (effective?.IsInforu == true)
                {
                    string apiBaseUrl = effective.ApiBaseUrl.HasValue() ? effective.ApiBaseUrl! : INFORU_DEFAULT_API_BASE_URL;
                    var result = await _inforuProvider.SendSmsAsync(text, phones, apiBaseUrl,
                        effective.Username, effective.ApiToken, effective.FromName, options, cancelToken);
                    if (!result.Success)
                        _logger.LogError($"Failed to send SMS via Inforu: {result.Error}");
                    return result;
                }

                // ActiveTrail has no per-message options (no DLR callback, no message id).
                var response = await _provider.SendSmsAsync(phones.First(), campaignName: CAMPAIGN_NAME, text, accountConfig, cancelToken);
                if (response == null || !response.IsSuccessful)
                {
                    _logger.LogError($"Failed to send SMS.");
                    var code = response?.HttpResponse != null ? (int)response.HttpResponse.StatusCode : 0;
                    return SmsSendResult.Fail($"ActiveTrail HTTP {code}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send SMS, Ex: {ex.ToString()}");
                throw;
            }

            return SmsSendResult.Ok();
        }

        private string ConfigureMessage(string text, Dictionary<string, string> tokens)
        {
            foreach (KeyValuePair<string, string> token in tokens)
                text = text.Replace(token.Key, token.Value);

            return text;
        }

        private async Task<bool> SendAttendanceCheckMessageAsync(MessageType MessageTypeId, List<string> phones, int languageId, string site, string readinessState, CancellationToken cancelToken = default)
        {
            //_logger.LogTrace($"Sending AttendanceCheckManager IVR to {phone}");

            if (!phones.HasValue())
                throw new GeorgeInvalidArgumentException("SMS phone list is empty.");

            // Build a the replacement tokens.
            Dictionary<string, string> tokens = new Dictionary<string, string>();
            tokens.Add("##site##", site);
            tokens.Add("##readinessState##", readinessState);

            return await SendAsync(phones, MessageTypeId, languageId, tokens, cancelToken);
        }

    }
}
