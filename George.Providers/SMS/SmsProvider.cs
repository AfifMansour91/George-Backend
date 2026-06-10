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
        /// <summary>Host only (e.g. app.example.com), no scheme. Last line of login OTP SMS becomes <c>@host #code</c> for Chrome Web OTP — must match the site origin.</summary>
        private static string _otpWebOriginHost = string.Empty;
        protected readonly ILogger<SmsProvider> _logger;
        protected readonly HttpHelper _httpHelper;

        //private Sms019Provider _provider;
        private ActiveTrailSmsProvider _provider;
        //private static Dictionary<string, string> _messageTemplates = new();


        //**************************    Construction    **************************//
        public SmsProvider(ILoggerFactory loggerFactory, ILogger<SmsProvider> logger, HttpHelper httpHelper)
        {
            _logger = logger;
            _httpHelper = httpHelper;
            _provider = new ActiveTrailSmsProvider(httpHelper);
        }


        //*************************    Properties    *************************//

        public static bool IsInitialized
        {
            get
            {
                //if (!_messageTemplates.HasValue())
                //    return false;

                return ActiveTrailSmsProvider.IsInitialized;
            }
        }


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
            VerifyInit();

            var phones = new List<string>() { phone };

            return await SendAsync(phones, text, cancelToken);
        }

        public async Task<bool> SendTextAsync(List<string> phones, string text, CancellationToken cancelToken = default)
        {
            VerifyInit();

            return await SendAsync(phones, text, cancelToken);
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
            return await SendAsync(phones, otpText, cancelToken);
        }

        //*************************    Private Methods    ************************//

        private void VerifyInit()
        {
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

            try
            {
                //var response = await _provider.SendSmsAsync(text, phones, cancelToken);
                var response = await _provider.SendSmsAsync(phones.First(), campaignName: CAMPAIGN_NAME, text, cancelToken);
                if (!response.IsSuccessful)
                {
                    _logger.LogError($"Failed to send SMS.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send SMS, Ex: {ex.ToString()}");
                throw;
            }

            return true;
        }

        private async Task<bool> SendAsync(List<string> phones, string text, CancellationToken cancelToken = default)
        {
            _logger.LogTrace($"Sending SMS to {phones.Count} phones (first one is {phones[0]}).");

            try
            {
                //var response = await _provider.SendSmsAsync(text, phones, cancelToken);
                var response = await _provider.SendSmsAsync(phones.First(), campaignName: CAMPAIGN_NAME, text, cancelToken);
                if (!response.IsSuccessful)
                {
                    _logger.LogError($"Failed to send SMS.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send SMS, Ex: {ex.ToString()}");
                throw;
            }

            return true;
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
