using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// Per-account SMS accounts: resolves an account's own SMS credentials and sends through them,
    /// falling back to the system-wide SMS account when the account has none configured.
    /// Fallback happens only when no (valid, enabled) account config exists - never on a send failure.
    /// </summary>
    public class AccountSmsService : ServiceBase
    {
        public const string DefaultProviderName = "ActiveTrail";
        private const string DefaultTestMessage = "הודעת בדיקה: חשבון ה-SMS שלך מוגדר ופעיל.";

        private readonly AccountStorage _accountStorage;
        private readonly SmsProvider _smsProvider;

        public AccountSmsService(
            ILogger<AccountSmsService> logger,
            IMapper mapper,
            CacheManager cache,
            AccountStorage accountStorage,
            SmsProvider smsProvider
        ) : base(logger, mapper, cache)
        {
            _accountStorage = accountStorage;
            _smsProvider = smsProvider;
        }


        //*************************    Sending    *************************//

        /// <summary>The account's effective SMS credentials, or null when it should use the system default.</summary>
        public async Task<SmsAccountConfig?> GetAccountConfigAsync(int accountId, CancellationToken cancelToken)
        {
            var entity = await _accountStorage.GetSmsSettingsAsync(accountId, cancelToken).ConfigureAwait(false);
            return MapToConfig(entity);
        }

        /// <summary>True when an SMS can go out for this account - via its own credentials or the system default.</summary>
        public async Task<bool> CanSendForAccountAsync(int accountId, CancellationToken cancelToken)
        {
            return SmsProvider.CanSendWith(await GetAccountConfigAsync(accountId, cancelToken).ConfigureAwait(false));
        }

        /// <summary>Send a text for the given account, using its own SMS account when configured (else system default).</summary>
        public async Task<bool> SendTextAsync(int accountId, string phone, string text, CancellationToken cancelToken)
        {
            var config = await GetAccountConfigAsync(accountId, cancelToken).ConfigureAwait(false);
            return await _smsProvider.SendTextAsync(phone, text, config, cancelToken).ConfigureAwait(false);
        }

        /// <summary>Enabled + valid row =&gt; config; anything else =&gt; null (system default).</summary>
        public static SmsAccountConfig? MapToConfig(AccountSmsSettings? settings)
        {
            if (settings == null || !settings.IsEnabled)
                return null;
            if (!string.Equals(settings.Provider?.Trim(), DefaultProviderName, StringComparison.OrdinalIgnoreCase))
                return null;

            var config = new SmsAccountConfig
            {
                ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ? null : settings.ApiBaseUrl.Trim(),
                ApiToken = settings.ApiToken?.Trim() ?? string.Empty,
                FromName = settings.FromName?.Trim() ?? string.Empty,
            };
            return config.IsValid ? config : null;
        }


        //*************************    Settings API    *************************//

        public async Task<IApiResponse<AccountSmsSettingsRes>> GetSettingsAsync(int accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var entity = await _accountStorage.GetSmsSettingsAsync(accountId, cancelToken);
            response.Data = BuildRes(accountId, entity);
            return response;
        }

        public async Task<IApiResponse<AccountSmsSettingsRes>> UpsertSettingsAsync(int accountId, AccountSmsSettingsReq req, CancellationToken cancelToken)
        {
            int? userId = _authUser != null && _authUser.Id > 0 ? (int?)_authUser.Id : null;
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var provider = string.IsNullOrWhiteSpace(req.Provider) ? DefaultProviderName : req.Provider.Trim();
            if (!string.Equals(provider, DefaultProviderName, StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, $"Unsupported SMS provider '{provider}'. Only '{DefaultProviderName}' is supported.");

            var existing = await _accountStorage.GetSmsSettingsAsync(accountId, cancelToken);

            // Empty token in the request keeps the stored one - the client only ever sees a masked token.
            var apiToken = string.IsNullOrWhiteSpace(req.ApiToken) ? existing?.ApiToken : req.ApiToken.Trim();
            var fromName = req.FromName?.Trim();

            if (req.IsEnabled)
            {
                if (string.IsNullOrWhiteSpace(apiToken))
                    return CreateResponse(response, StatusCode.InvalidRequest, "API token is required to enable a per-account SMS account.");
                if (string.IsNullOrWhiteSpace(fromName))
                    return CreateResponse(response, StatusCode.InvalidRequest, "Sender name (FromName) is required to enable a per-account SMS account.");
            }

            var entity = new AccountSmsSettings
            {
                AccountId = accountId,
                IsEnabled = req.IsEnabled,
                Provider = DefaultProviderName,
                ApiBaseUrl = string.IsNullOrWhiteSpace(req.ApiBaseUrl) ? null : req.ApiBaseUrl.Trim(),
                ApiToken = apiToken,
                FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName,
                SourcePhone = string.IsNullOrWhiteSpace(req.SourcePhone) ? null : req.SourcePhone.Trim(),
                CreationUserId = userId,
                UpdateUserId = userId,
            };

            var saved = await _accountStorage.UpsertSmsSettingsAsync(entity, cancelToken);
            response.Data = BuildRes(accountId, saved);
            return response;
        }

        /// <summary>Remove the account's SMS credentials so it goes back to the system-wide SMS account.</summary>
        public async Task<IApiResponse<AccountSmsSettingsRes>> DeleteSettingsAsync(int accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            await _accountStorage.DeleteSmsSettingsAsync(accountId, cancelToken);
            response.Data = BuildRes(accountId, entity: null);
            return response;
        }

        /// <summary>Send a test SMS using the account's SAVED settings (save first, then test).</summary>
        public async Task<IApiResponse<AccountSmsTestRes>> SendTestAsync(int accountId, AccountSmsTestReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsTestRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var phone = req.Phone?.Trim();
            if (string.IsNullOrWhiteSpace(phone))
                return CreateResponse(response, StatusCode.InvalidRequest, "Phone is required.");

            var config = await GetAccountConfigAsync(accountId, cancelToken);
            if (!SmsProvider.CanSendWith(config))
                return CreateResponse(response, StatusCode.InvalidRequest, "SMS is not configured (neither account credentials nor system default).");

            var text = string.IsNullOrWhiteSpace(req.Message) ? DefaultTestMessage : req.Message.Trim();

            bool sent;
            try
            {
                sent = await _smsProvider.SendTextAsync(phone, text, config, cancelToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Test SMS failed for account {AccountId}.", accountId);
                return CreateResponse(response, StatusCode.InvalidRequest, "SMS send failed. Check the credentials and try again.");
            }

            if (!sent)
                return CreateResponse(response, StatusCode.InvalidRequest, "SMS send failed. Check the credentials and try again.");

            response.Data = new AccountSmsTestRes { Sent = true, UsedAccountConfig = config != null };
            return response;
        }


        //*************************    Private Methods    *************************//

        private static AccountSmsSettingsRes BuildRes(int accountId, AccountSmsSettings? entity)
        {
            var effectiveConfig = MapToConfig(entity);
            return new AccountSmsSettingsRes
            {
                AccountId = accountId,
                IsConfigured = entity != null,
                IsEnabled = entity?.IsEnabled ?? false,
                Provider = entity?.Provider ?? DefaultProviderName,
                ApiBaseUrl = entity?.ApiBaseUrl,
                HasApiToken = !string.IsNullOrWhiteSpace(entity?.ApiToken),
                ApiTokenMasked = MaskSecret(entity?.ApiToken),
                FromName = entity?.FromName,
                SourcePhone = entity?.SourcePhone,
                UsingSystemDefault = effectiveConfig == null,
            };
        }

        /// <summary>"••••" + last 4 chars; null for empty. Never return the full secret to the client.</summary>
        public static string? MaskSecret(string? secret)
        {
            var s = secret?.Trim();
            if (string.IsNullOrEmpty(s))
                return null;
            return s.Length <= 4 ? "••••" : "••••" + s[^4..];
        }
    }
}
