using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Marketing;
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
        public const string DefaultProviderName = SmsProviderNames.ActiveTrail;
        private static readonly string[] SupportedProviders = { SmsProviderNames.ActiveTrail, SmsProviderNames.Inforu };
        private const string DefaultTestMessage = "הודעת בדיקה: חשבון ה-SMS שלך מוגדר ופעיל.";
        public const string ErrManagedByPlatform = "חשבון ה-SMS של העסק מנוהל על ידי ג'ורג'יו ולא ניתן לשינוי מכאן.";
        public const string ErrSystemAccountPlatformOnly = "רק מנהל המערכת יכול להעביר את העסק לחשבון ה-SMS של המערכת.";
        public const string ErrInforuPlatformOnly = "InforU זמין רק כתת-חשבון שג'ורג'יו פותחת עבור העסק — לא ניתן לחבר חשבון InforU עצמאי.";

        private readonly AccountStorage _accountStorage;
        private readonly UserStorage? _userStorage;
        private readonly Marketing.InforuQuotaService? _inforuQuota;
        private readonly SmsProvider _smsProvider;
        private readonly IMessageLogQueue? _messageLogQueue;

        public AccountSmsService(
            ILogger<AccountSmsService> logger,
            IMapper mapper,
            CacheManager cache,
            AccountStorage accountStorage,
            SmsProvider smsProvider,
            IMessageLogQueue? messageLogQueue = null,
            UserStorage? userStorage = null,
            Marketing.InforuQuotaService? inforuQuota = null
        ) : base(logger, mapper, cache)
        {
            _userStorage = userStorage;
            _inforuQuota = inforuQuota;
            _accountStorage = accountStorage;
            _smsProvider = smsProvider;
            _messageLogQueue = messageLogQueue;
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

        /// <summary>
        /// Sends through <paramref name="config"/> (null = system default) and records the attempt in MessageLog.
        /// Every SMS of the product goes through here so it can be counted per account, kind and category.
        /// Behaves exactly like <see cref="SmsProvider.SendTextAsync(string, string, SmsAccountConfig?, CancellationToken)"/>: same result, same exceptions.
        /// </summary>
        public async Task<bool> SendLoggedAsync(SmsLogContext context, string phone, string text, SmsAccountConfig? config, CancellationToken cancelToken)
        {
            return (await SendLoggedDetailedAsync(context, phone, text, config, options: null, cancelToken).ConfigureAwait(false)).Success;
        }

        /// <summary>Like <see cref="SendLoggedAsync"/> but returns the provider verdict (error text, quota-exceeded) and accepts per-message options.</summary>
        public async Task<SmsSendResult> SendLoggedDetailedAsync(SmsLogContext context, string phone, string text, SmsAccountConfig? config, SmsSendOptions? options, CancellationToken cancelToken)
        {
            SmsSendResult? result = null;
            string? error = null;
            try
            {
                result = await _smsProvider.SendTextDetailedAsync(phone, text, config, options, cancelToken).ConfigureAwait(false);
                if (!result.Success)
                    error = result.Error ?? "provider rejected";
                return result;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                throw;
            }
            finally
            {
                LogSend(context, phone, text, config, result?.Success == true, error);
            }
        }

        /// <summary>Record an SMS that was sent outside <see cref="SendLoggedAsync"/> (OTP). Best-effort; never throws.</summary>
        public void LogSend(SmsLogContext context, string? phone, string? text, SmsAccountConfig? config, bool success, string? error = null)
        {
            if (_messageLogQueue == null)
                return;
            try
            {
                var usedAccountConfig = config?.IsValid == true;
                _messageLogQueue.TryEnqueue(new MessageLog
                {
                    CreationTime = DateTime.UtcNow,
                    AccountId = context.AccountId,
                    SiteId = context.SiteId,
                    Kind = context.Kind,
                    Category = context.Category,
                    Channel = "sms",
                    NormalizedPhone = MarketingStorage.NormalizePhone(phone),
                    Units = SmsUnits.Calculate(text),
                    Provider = usedAccountConfig ? config!.Provider : SmsProvider.SystemProviderName,
                    UsedAccountConfig = usedAccountConfig,
                    Success = success,
                    Error = error != null && error.Length > 500 ? error.Substring(0, 500) : error,
                    OrderId = context.OrderId,
                    MarketingDeliveryId = context.MarketingDeliveryId,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MessageLog enqueue failed.");
            }
        }

        /// <summary>Enabled + valid row =&gt; config; anything else =&gt; null (system default).</summary>
        public static SmsAccountConfig? MapToConfig(AccountSmsSettings? settings)
        {
            if (settings == null || !settings.IsEnabled)
                return null;
            var provider = NormalizeProvider(settings.Provider);
            if (provider == null)
                return null;

            var config = new SmsAccountConfig
            {
                Provider = provider,
                ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ? null : settings.ApiBaseUrl.Trim(),
                Username = settings.Username?.Trim() ?? string.Empty,
                ApiToken = settings.ApiToken?.Trim() ?? string.Empty,
                FromName = settings.FromName?.Trim() ?? string.Empty,
                BilledByPlatform = settings.BilledByPlatform,
                InforuCustomerId = string.IsNullOrWhiteSpace(settings.InforuCustomerId) ? null : settings.InforuCustomerId.Trim(),
            };
            return config.IsValid ? config : null;
        }

        /// <summary>Inforu sender rule (apidoc): up to 11 consecutive characters, no spaces (optionally starting with *), or a whitelisted phone number of up to 14 digits.</summary>
        public static bool IsValidInforuSender(string? sender)
        {
            var s = sender?.Trim() ?? string.Empty;
            if (s.Length == 0)
                return false;
            if (s.All(char.IsDigit))
                return s.Length <= 14;
            var body = s.StartsWith('*') ? s.Substring(1) : s;
            return body.Length is > 0 and <= 11 && body.All(ch => ch < 128 && !char.IsWhiteSpace(ch));
        }

        /// <summary>Canonical provider name for a supported provider (case-insensitive); null when unsupported.</summary>
        public static string? NormalizeProvider(string? provider)
        {
            var name = string.IsNullOrWhiteSpace(provider) ? DefaultProviderName : provider.Trim();
            return SupportedProviders.FirstOrDefault(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        }


        //*************************    Settings API    *************************//

        public async Task<IApiResponse<AccountSmsSettingsRes>> GetSettingsAsync(int accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = await BuildFullResAsync(accountId, cancelToken);
            return response;
        }

        /// <summary>Choose which saved row sends: "ActiveTrail" | "Inforu" | null = system default. The shop may choose; it just cannot edit Giorgio's row.</summary>
        public async Task<IApiResponse<AccountSmsSettingsRes>> SetActiveProviderAsync(int accountId, AccountSmsActiveProviderReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            string? provider = null;
            // Falling back to the SYSTEM account is the platform's decision (it is Giorgio's own sender and cost), not the shop's.
            if (string.IsNullOrWhiteSpace(req.Provider) && !await IsUnrestrictedCallerAsync(cancelToken))
                return CreateResponse(response, StatusCode.UnauthorizedData, ErrSystemAccountPlatformOnly);
            if (!string.IsNullOrWhiteSpace(req.Provider))
            {
                provider = NormalizeProvider(req.Provider);
                if (provider == null)
                    return CreateResponse(response, StatusCode.InvalidRequest, $"Unsupported SMS provider '{req.Provider}'.");
                var row = await _accountStorage.GetSmsSettingsRowAsync(accountId, provider, cancelToken);
                if (row == null)
                    return CreateResponse(response, StatusCode.InvalidRequest, "אין פרטי חיבור שמורים לספק הזה.");
                row.IsEnabled = true;
                if (MapToConfig(row) == null)
                    return CreateResponse(response, StatusCode.InvalidRequest, "פרטי החיבור של הספק הזה לא שלמים.");
            }

            await _accountStorage.SetActiveSmsProviderAsync(accountId, provider, cancelToken);
            response.Data = await BuildFullResAsync(accountId, cancelToken);
            return response;
        }

        public async Task<IApiResponse<AccountSmsSettingsRes>> UpsertSettingsAsync(int accountId, AccountSmsSettingsReq req, CancellationToken cancelToken)
        {
            int? userId = _authUser != null && _authUser.Id > 0 ? (int?)_authUser.Id : null;
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var provider = NormalizeProvider(req.Provider);
            if (provider == null)
                return CreateResponse(response, StatusCode.InvalidRequest, $"Unsupported SMS provider '{req.Provider}'. Supported: {string.Join(", ", SupportedProviders)}.");

            var existing = await _accountStorage.GetSmsSettingsRowAsync(accountId, provider, cancelToken);
            var unrestricted = await IsUnrestrictedCallerAsync(cancelToken);
            bool isInforu = string.Equals(provider, SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase);

            // InforU is provisioned by the platform only (a sub-account under its parent): a shop can neither bring its
            // own nor edit the row Giorgio manages. Its own ActiveTrail row is its own business.
            if (!unrestricted && (isInforu || existing?.BilledByPlatform == true))
                return CreateResponse(response, StatusCode.InvalidRequest, isInforu ? ErrInforuPlatformOnly : ErrManagedByPlatform);

            // Empty token in the request keeps the stored one - the client only ever sees a masked token.
            var apiToken = string.IsNullOrWhiteSpace(req.ApiToken) ? existing?.ApiToken : req.ApiToken.Trim();
            var username = string.IsNullOrWhiteSpace(req.Username) ? existing?.Username : req.Username.Trim();
            var fromName = req.FromName?.Trim();

            if (req.IsEnabled)
            {
                if (string.IsNullOrWhiteSpace(apiToken))
                    return CreateResponse(response, StatusCode.InvalidRequest, "API token is required to enable a per-account SMS account.");
                if (string.IsNullOrWhiteSpace(fromName))
                    return CreateResponse(response, StatusCode.InvalidRequest, "Sender name (FromName) is required to enable a per-account SMS account.");
                if (isInforu && string.IsNullOrWhiteSpace(username))
                    return CreateResponse(response, StatusCode.InvalidRequest, "Username is required for the Inforu provider.");
                if (isInforu && !IsValidInforuSender(fromName))
                    return CreateResponse(response, StatusCode.InvalidRequest, "שם השולח ב-InforU: עד 11 תווים באנגלית ללא רווחים, או מספר טלפון (עד 14 ספרות) שאושר ברשימה הלבנה.");
            }

            // Who pays the provider is the platform's call, not the shop's: only master / system admin may flip it.
            var billedByPlatform = existing?.BilledByPlatform ?? false;
            var inforuCustomerId = existing?.InforuCustomerId;
            if ((req.BilledByPlatform.HasValue || req.InforuCustomerId != null) && unrestricted)
            {
                if (req.BilledByPlatform.HasValue)
                    billedByPlatform = req.BilledByPlatform.Value;
                if (req.InforuCustomerId != null)
                    inforuCustomerId = string.IsNullOrWhiteSpace(req.InforuCustomerId) ? null : req.InforuCustomerId.Trim();
            }

            var entity = new AccountSmsSettings
            {
                AccountId = accountId,
                IsEnabled = req.IsEnabled,
                Provider = provider,
                ApiBaseUrl = string.IsNullOrWhiteSpace(req.ApiBaseUrl) ? null : req.ApiBaseUrl.Trim(),
                Username = string.IsNullOrWhiteSpace(username) ? null : username,
                ApiToken = apiToken,
                FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName,
                SourcePhone = string.IsNullOrWhiteSpace(req.SourcePhone) ? null : req.SourcePhone.Trim(),
                BilledByPlatform = billedByPlatform,
                InforuCustomerId = inforuCustomerId,
                CreationUserId = userId,
                UpdateUserId = userId,
            };

            await _accountStorage.UpsertSmsSettingsAsync(entity, cancelToken);
            response.Data = await BuildFullResAsync(accountId, cancelToken);
            return response;
        }

        /// <summary>Remove one provider's credentials. A shop may remove only its own ActiveTrail; Giorgio's Inforu row is the platform's.</summary>
        public async Task<IApiResponse<AccountSmsSettingsRes>> DeleteSettingsAsync(int accountId, string? providerName, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountSmsSettingsRes>();
            var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
            if (account == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var provider = NormalizeProvider(providerName);
            if (provider == null)
                return CreateResponse(response, StatusCode.InvalidRequest, $"Unsupported SMS provider '{providerName}'.");

            var existing = await _accountStorage.GetSmsSettingsRowAsync(accountId, provider, cancelToken);
            if (existing == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "אין פרטי חיבור שמורים לספק הזה.");
            var isInforu = string.Equals(provider, SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase);
            if ((isInforu || existing.BilledByPlatform) && !await IsUnrestrictedCallerAsync(cancelToken))
                return CreateResponse(response, StatusCode.UnauthorizedData, ErrManagedByPlatform);

            await _accountStorage.DeleteSmsSettingsAsync(accountId, provider, cancelToken);
            response.Data = await BuildFullResAsync(accountId, cancelToken);
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
                sent = await SendLoggedAsync(SmsLogContext.System(MessageCategory.Test, accountId), phone, text, config, cancelToken);
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

        private async Task<AccountSmsSettingsRes> BuildFullResAsync(int accountId, CancellationToken cancelToken)
        {
            var rows = await _accountStorage.GetSmsSettingsRowsAsync(accountId, cancelToken);
            var active = rows.FirstOrDefault(r => r.IsEnabled);
            var res = BuildRes(accountId, active);
            res.ActiveProvider = active == null ? null : NormalizeProvider(active.Provider);
            var inforu = rows.FirstOrDefault(r => string.Equals(NormalizeProvider(r.Provider), SmsProviderNames.Inforu, StringComparison.OrdinalIgnoreCase));
            var activeTrail = rows.FirstOrDefault(r => string.Equals(NormalizeProvider(r.Provider), SmsProviderNames.ActiveTrail, StringComparison.OrdinalIgnoreCase));
            res.Inforu = inforu == null ? null : BuildProviderRes(inforu);
            res.ActiveTrail = activeTrail == null ? null : BuildProviderRes(activeTrail);

            // Live balance of the Inforu sub-account - whatever row is active, the shop wants to see what it has there.
            if (inforu != null && _inforuQuota != null)
            {
                var probe = new AccountSmsSettings { AccountId = inforu.AccountId, IsEnabled = true, Provider = inforu.Provider, ApiBaseUrl = inforu.ApiBaseUrl, Username = inforu.Username, ApiToken = inforu.ApiToken, FromName = inforu.FromName, BilledByPlatform = inforu.BilledByPlatform, InforuCustomerId = inforu.InforuCustomerId };
                var config = MapToConfig(probe);
                if (config != null)
                {
                    var q = await _inforuQuota.GetAsync(accountId, config, cancelToken);
                    res.InforuQuota = new AccountSmsQuotaRes { Available = q.Available, Remaining = q.Remaining, QuotaType = q.QuotaType, Error = q.Error };
                    if (res.Inforu != null && string.IsNullOrWhiteSpace(res.Inforu.InforuCustomerId))
                        res.Inforu.InforuCustomerId = q.CustomerId;
                }
            }
            return res;
        }

        private static AccountSmsProviderRes BuildProviderRes(AccountSmsSettings row)
        {
            var asEnabled = new AccountSmsSettings { Provider = row.Provider, IsEnabled = true, ApiBaseUrl = row.ApiBaseUrl, Username = row.Username, ApiToken = row.ApiToken, FromName = row.FromName };
            return new AccountSmsProviderRes
            {
                Provider = NormalizeProvider(row.Provider) ?? row.Provider,
                IsEnabled = row.IsEnabled,
                Username = row.Username,
                FromName = row.FromName,
                HasApiToken = !string.IsNullOrWhiteSpace(row.ApiToken),
                ApiTokenMasked = MaskSecret(row.ApiToken),
                BilledByPlatform = row.BilledByPlatform,
                InforuCustomerId = row.InforuCustomerId,
                IsValid = MapToConfig(asEnabled) != null,
            };
        }

        private static AccountSmsSettingsRes BuildRes(int accountId, AccountSmsSettings? entity)
        {
            var effectiveConfig = MapToConfig(entity);
            return new AccountSmsSettingsRes
            {
                AccountId = accountId,
                IsConfigured = entity != null,
                IsEnabled = entity?.IsEnabled ?? false,
                Provider = NormalizeProvider(entity?.Provider) ?? entity?.Provider ?? SmsProvider.SystemProviderName,
                SystemProvider = SmsProvider.SystemProviderName,
                ApiBaseUrl = entity?.ApiBaseUrl,
                Username = entity?.Username,
                HasApiToken = !string.IsNullOrWhiteSpace(entity?.ApiToken),
                ApiTokenMasked = MaskSecret(entity?.ApiToken),
                FromName = entity?.FromName,
                SourcePhone = entity?.SourcePhone,
                BilledByPlatform = entity?.BilledByPlatform ?? false,
                InforuCustomerId = entity?.InforuCustomerId,
                UsingSystemDefault = effectiveConfig == null,
            };
        }

        private async Task<bool> IsUnrestrictedCallerAsync(CancellationToken cancelToken)
        {
            if (AuthUser.IsMaster)
                return true;
            if (_userStorage == null || AuthUser.Id <= 0)
                return false;
            var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
            return user?.RoleId == (int)UserRole.Admin;
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
