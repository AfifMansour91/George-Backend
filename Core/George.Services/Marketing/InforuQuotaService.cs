using George.Data;
using George.Providers;
using George.Services.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Keeps George's marketing bank and the Inforu sub-account quota in step (apidoc.inforu.co.il → General → Get Quota /
/// Create Or Add Quota). Reading uses the SHOP's sub-account credentials (an empty Level returns its own customer
/// quota - and its customer id, which we remember). Adding needs the PLATFORM's parent credentials
/// (<c>Sms:Inforu:ParentUsername</c> / <c>Sms:Inforu:ParentApiToken</c>): a customer-level user can only top up what is below it.
/// </summary>
public class InforuQuotaService
{
    public const string LevelCustomer = "Customer";

    private readonly SmsProvider _smsProvider;
    private readonly AccountStorage _accountStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InforuQuotaService> _logger;

    public InforuQuotaService(SmsProvider smsProvider, AccountStorage accountStorage, IConfiguration configuration, ILogger<InforuQuotaService> logger)
    {
        _smsProvider = smsProvider;
        _accountStorage = accountStorage;
        _configuration = configuration;
        _logger = logger;
    }

    public InforuParentCredentials? ParentCredentials
    {
        get
        {
            // Giorgio's own Inforu account (Sms:Inforu:Username/ApiToken, also the system sender) is the parent of every sub-account;
            // Sms:Inforu:ParentUsername/ParentApiToken override it when the top-ups must come from a different user.
            var username = _configuration["Sms:Inforu:ParentUsername"] ?? _configuration["Sms:Inforu:Username"];
            var token = _configuration["Sms:Inforu:ParentApiToken"] ?? _configuration["Sms:Inforu:ApiToken"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(token))
                return null;
            return new InforuParentCredentials
            {
                Username = username.Trim(),
                ApiToken = token.Trim(),
                ApiBaseUrl = string.IsNullOrWhiteSpace(_configuration["Sms:Inforu:ApiBaseUrl"]) ? null : _configuration["Sms:Inforu:ApiBaseUrl"]!.Trim(),
            };
        }
    }

    /// <summary>Live remaining SMS quota of the shop's Inforu sub-account. Never throws - a provider hiccup must not break the settings screen.</summary>
    public async Task<MarketingProviderQuotaRes> GetAsync(int accountId, SmsAccountConfig config, CancellationToken cancelToken)
    {
        var res = new MarketingProviderQuotaRes
        {
            Provider = SmsProviderNames.Inforu,
            CustomerId = config.InforuCustomerId,
            CanAllocate = ParentCredentials != null,
        };
        try
        {
            var info = await _smsProvider.GetInforuQuotaAsync(config, cancelToken).ConfigureAwait(false);
            if (!info.Success)
            {
                res.Error = info.Error;
                return res;
            }
            res.Available = true;
            res.Remaining = info.RemainingSms;
            res.QuotaType = info.QuotaType;
            res.WarningLevel = info.WarningLevel;

            // The sub-account's own quota answer tells us which Inforu customer it is - remember it for allocations.
            if (string.IsNullOrWhiteSpace(config.InforuCustomerId) && string.Equals(info.Level, LevelCustomer, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(info.LevelValue))
            {
                await _accountStorage.SetInforuCustomerIdAsync(accountId, info.LevelValue!, cancelToken).ConfigureAwait(false);
                config.InforuCustomerId = info.LevelValue;
                res.CustomerId = info.LevelValue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inforu GetQuota failed for account {AccountId}.", accountId);
            res.Error = ex.Message;
        }
        res.CanAllocate = res.CanAllocate && !string.IsNullOrWhiteSpace(res.CustomerId);
        return res;
    }

    /// <summary>Adds <paramref name="amount"/> SMS to the sub-account's package quota in Inforu. Returns a user-facing error, or null on success.</summary>
    public async Task<(string? Error, int? RemainingAfter)> AddAsync(int accountId, SmsAccountConfig config, int amount, CancellationToken cancelToken)
    {
        var parent = ParentCredentials;
        if (parent == null)
            return ("חשבון-האב של InforU לא מוגדר בשרת (Sms:Inforu:Username / ApiToken) - המכסה נטענה בג'ורג'יו בלבד.", null);

        if (string.IsNullOrWhiteSpace(config.InforuCustomerId))
        {
            var probe = await GetAsync(accountId, config, cancelToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(config.InforuCustomerId))
                return ($"לא הצלחתי לזהות את מספר הלקוח של החנות ב-InforU ({probe.Error ?? "אין תשובה"}) - המכסה נטענה בג'ורג'יו בלבד.", null);
        }

        // CreateOrAddQuota ADDS only to a "Packages" quota; on a monthly-renewal quota the amount would OVERWRITE the
        // monthly allowance (Inforu docs). Those sub-accounts are topped up in the Inforu portal - never blindly from here.
        var current = await GetAsync(accountId, config, cancelToken).ConfigureAwait(false);
        if (current.Available && !string.IsNullOrWhiteSpace(current.QuotaType) && !current.QuotaType.StartsWith("Package", StringComparison.OrdinalIgnoreCase))
            return ($"המכסה של תת-החשבון ב-InforU היא מסוג \"{current.QuotaType}\" (לא חבילה) - טעינה מכאן הייתה דורסת את ההקצאה החודשית. יש לטעון בפורטל InforU.", null);

        try
        {
            var outcome = await _smsProvider.AddInforuQuotaAsync(parent, LevelCustomer, config.InforuCustomerId!, amount, cancelToken).ConfigureAwait(false);
            if (!outcome.Success)
                return ($"InforU דחתה את הטעינה: {outcome.Error} - המכסה נטענה בג'ורג'יו בלבד.", null);
            _logger.LogInformation("Inforu quota +{Amount} for account {AccountId} (customer {CustomerId}): {Before} → {After}.",
                amount, accountId, config.InforuCustomerId, outcome.RemainingBefore, outcome.RemainingAfter);
            return (null, outcome.RemainingAfter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inforu CreateOrAddQuota failed for account {AccountId}.", accountId);
            return ($"שגיאה בטעינה ל-InforU: {ex.Message} - המכסה נטענה בג'ורג'יו בלבד.", null);
        }
    }
}
