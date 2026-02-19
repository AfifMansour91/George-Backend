using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace George.Providers.Twilio
{
    /// <summary>
    /// Sends OTP via Twilio voice call (TTS). Configure Twilio:AccountSid, Twilio:AuthToken, Twilio:VoiceFromNumber in appsettings.
    /// </summary>
    public class TwilioVoiceOtpProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwilioVoiceOtpProvider> _logger;

        private const string ConfigAccountSid = "Twilio:AccountSid";
        private const string ConfigAuthToken = "Twilio:AuthToken";
        private const string ConfigVoiceFromNumber = "Twilio:VoiceFromNumber";

        public TwilioVoiceOtpProvider(IConfiguration configuration, ILogger<TwilioVoiceOtpProvider> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Whether Twilio credentials and VoiceFromNumber are configured.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                var accountSid = _configuration[ConfigAccountSid];
                var authToken = _configuration[ConfigAuthToken];
                var from = _configuration[ConfigVoiceFromNumber];
                return !string.IsNullOrWhiteSpace(accountSid) && !string.IsNullOrWhiteSpace(authToken) && !string.IsNullOrWhiteSpace(from);
            }
        }

        /// <summary>
        /// Place a voice call to the given phone number and speak the OTP code.
        /// </summary>
        /// <param name="phone">Phone number (will be normalized to E.164; e.g. 0501234567 → +972501234567).</param>
        /// <param name="otp">6-digit OTP code to speak.</param>
        /// <param name="languageId">Optional; 1 = Hebrew, others = English.</param>
        /// <param name="cancelToken">Cancellation token.</param>
        /// <returns>True if the call was initiated successfully.</returns>
        public async Task<bool> SendOtpByVoiceAsync(string phone, string otp, int languageId = 1, CancellationToken cancelToken = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Twilio voice OTP is not configured. Set Twilio:AccountSid, Twilio:AuthToken, Twilio:VoiceFromNumber.");
                return false;
            }

            var accountSid = _configuration[ConfigAccountSid]!.Trim();
            var authToken = _configuration[ConfigAuthToken]!.Trim();
            var fromNumber = _configuration[ConfigVoiceFromNumber]!.Trim();

            TwilioClient.Init(accountSid, authToken);

            var toE164 = NormalizeToE164(phone);
            if (string.IsNullOrEmpty(toE164))
            {
                _logger.LogWarning("Invalid phone number for voice OTP: {Phone}", phone);
                return false;
            }

            // Build TwiML: speak the OTP (optionally in Hebrew).
            var voiceResponse = new VoiceResponse();
            string sayText;
            string language = languageId == 1 ? "he-IL" : "en-US";
            if (languageId == 1)
            {
                // Hebrew: "קוד האימות שלך הוא X X X X X X"
                sayText = $"קוד האימות שלך הוא {SpellDigits(otp)}.";
            }
            else
            {
                sayText = $"Your verification code is {SpellDigits(otp)}.";
            }

            voiceResponse.Say(sayText, language: language);

            string twiml = voiceResponse.ToString();

            try
            {
                var call = await CallResource.CreateAsync(
                    to: new PhoneNumber(toE164),
                    from: new PhoneNumber(fromNumber),
                    twiml: new Twiml(twiml),
                    client: null
                ).ConfigureAwait(false);
                _logger.LogInformation("Twilio voice OTP call initiated to {To}, Sid: {Sid}", toE164, call.Sid);
                return call.Sid != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate Twilio voice OTP call to {Phone}", toE164);
                return false;
            }
        }

        /// <summary>
        /// Normalize phone to E.164 (e.g. 0501234567 → +972501234567).
        /// </summary>
        private static string? NormalizeToE164(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new System.Text.StringBuilder();
            foreach (char c in phone)
            {
                if (char.IsDigit(c)) digits.Append(c);
            }
            var s = digits.ToString();
            if (s.Length < 9) return null;
            if (s.StartsWith('0') && s.Length == 10)
                return "+972" + s[1..];
            if (s.StartsWith("972") && s.Length >= 11)
                return "+" + s;
            if (s.Length >= 9 && s.Length <= 11)
                return "+972" + s.TrimStart('0');
            return "+" + s;
        }

        /// <summary>
        /// Spell digits with spaces so TTS reads them clearly (e.g. "123456" → "1 2 3 4 5 6").
        /// </summary>
        private static string SpellDigits(string otp)
        {
            if (string.IsNullOrEmpty(otp)) return otp;
            return string.Join(" ", otp.ToCharArray());
        }
    }
}
