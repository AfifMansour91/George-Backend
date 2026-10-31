using System.Text.Json;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using George.Services.Marketing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>
/// The API side of the AI Product Studio (spec §9): gate + scope, create/read/cancel/retry/rate jobs. Generation itself runs in
/// <see cref="AiStudioJobRunner"/> under <see cref="AiStudioJobHostedService"/>, so a request returns in milliseconds and the phone keeps
/// filling in the form.
/// </summary>
public class AiStudioService : ServiceBase
{
    public const string ErrDisabled = "סטודיו ה-AI אינו פעיל בחשבון זה";
    private const string ErrNoAccount = "לא נמצא חשבון לפעולה";
    private const string ErrNoJob = "העבודה לא נמצאה";
    private const string ErrUnknownLook = "מראה לא מוכר";
    private const string ErrNoSource = "חסרה תמונת מקור";
    private const string ErrNoText = "כדי ליצור תמונה צריך שם מוצר או תיאור של מה שרואים בתמונה";
    private const string ErrDailyCap = "הגעתם למכסת התמונות היומית של החשבון. נסו שוב מחר או פנו אלינו להגדלת המכסה.";
    private const string ErrNoProvider = "אף מנוע תמונה לא מוגדר בשרת (AiStudio:OpenAI / AiStudio:Gemini)";
    public const string ErrNoCredits = "אין מספיק קרדיטים בחשבון. אפשר לרכוש חבילה בהגדרות › סטודיו AI.";
    public const string ErrNoCreditsExisting = "שיפור של תמונה קיימת מחויב בקרדיטים גם במצב הקהילתי, ואין מספיק קרדיטים בחשבון. אפשר לרכוש חבילה בהגדרות › סטודיו AI.";
    public const string ErrConsentRequired = "לפני השימוש הראשון יש לאשר את תנאי המאגר המשותף.";
    private const string ErrLookOff = "המראה הזה כבוי בחשבון (הגדרות › סטודיו AI › המראים שלי).";

    private readonly AiStudioStorage _storage;
    private readonly UserStorage _userStorage;
    private readonly AccountStorage _accountStorage;
    private readonly SiteAccessService _siteAccess;
    private readonly IEnumerable<IImageModelProvider> _providers;
    private readonly ProductVisionService _vision;
    private readonly IAiStudioRealtimeNotifier _notifier;
    private readonly AiStudioOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiCreditStorage _credits;
    private readonly IConfiguration _configuration;
    private readonly SiteStorage _siteStorage;
    private readonly MediaStorage _mediaStorage;
    private readonly ProductStorage _productStorage;

    public AiStudioService(
        ILogger<AiStudioService> logger,
        IMapper mapper,
        CacheManager cache,
        AiStudioStorage storage,
        UserStorage userStorage,
        AccountStorage accountStorage,
        SiteAccessService siteAccess,
        IEnumerable<IImageModelProvider> providers,
        ProductVisionService vision,
        IAiStudioRealtimeNotifier notifier,
        IOptions<AiStudioOptions> options,
        IHttpClientFactory httpClientFactory,
        AiCreditStorage credits,
        IConfiguration configuration,
        SiteStorage siteStorage,
        MediaStorage mediaStorage,
        ProductStorage productStorage) : base(logger, mapper, cache)
    {
        _mediaStorage = mediaStorage;
        _productStorage = productStorage;
        _siteStorage = siteStorage;
        _httpClientFactory = httpClientFactory;
        _credits = credits;
        _configuration = configuration;
        _storage = storage;
        _userStorage = userStorage;
        _accountStorage = accountStorage;
        _siteAccess = siteAccess;
        _providers = providers;
        _vision = vision;
        _notifier = notifier;
        _options = options.Value;
    }

    //*************************    Status    *************************//

    public async Task<IApiResponse<AiStudioStatusRes>> GetStatusAsync(AiStudioScopeReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiStudioStatusRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: false, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);

        var cap = scope.Account.AiStudioDailyCap ?? _options.DefaultDailyCap;
        var statusMode = ModeOf(scope.Account);
        // The balance matters in community mode too: existing-image jobs are paid there as well (George's rule, 31.10.2026).
        var statusBalance = scope.Account.AiStudioEnabled ? await _credits.GetBalanceAsync(scope.AccountId, cancelToken).ConfigureAwait(false) : 0;
        await EnsurePresetsAppliedAsync(cancelToken).ConfigureAwait(false);
        var accountLooks = await ResolveAccountLooksAsync(scope.AccountId, cancelToken).ConfigureAwait(false);
        response.Data = new AiStudioStatusRes
        {
            HdCredits = _options.HdCredits,
            ExistingImagePaid = _options.ExistingImageAlwaysPaid,
            WebSearchCredits = _options.WebSearchCredits,
            Defaults = await ResolveDefaultsAsync(scope, cancelToken).ConfigureAwait(false),
            Balance = statusBalance,
            CanPurchase = PlatformBilling.FromConfiguration(_configuration) != null,
            PoolConsentAt = scope.Account.AiStudioPoolConsentAt,
            RequiresPoolConsent = scope.Account.AiStudioEnabled && statusMode == Account.AiStudioModeCommunity && scope.Account.AiStudioPoolConsentAt == null,
            Enabled = scope.Account.AiStudioEnabled,
            AccountId = scope.AccountId,
            Mode = ModeOf(scope.Account),
            DailyCap = cap,
            UsedToday = scope.Account.AiStudioEnabled ? await _storage.CountJobsSinceAsync(scope.AccountId, DateTime.UtcNow.Date, cancelToken).ConfigureAwait(false) : 0,
            VisionEnabled = _vision.IsConfigured,
            DefaultProvider = _options.DefaultProvider,
            Looks = accountLooks.Where(l => l.IsActive).Select(l => new AiLookRes { Key = l.Key, Label = l.Label, Description = l.Description, Credits = l.Credits, NeedsSource = l.NeedsSource, IsBackground = AiStudioLooks.IsBackground(l.Key) }).ToList(),
        };
        return response;
    }

    /// <summary>The platform's looks with the account's own preset rows applied (a look the shop switched off is inactive here).</summary>
    private async Task<List<AiStudioLooks.LookDef>> ResolveAccountLooksAsync(int accountId, CancellationToken cancelToken)
    {
        List<AiStylePreset> rows;
        try
        {
            rows = await _storage.GetAccountPresetsAsync(accountId, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio: account presets could not be loaded for {AccountId}", accountId);
            rows = new List<AiStylePreset>();
        }
        var byLook = rows.ToDictionary(r => r.Look, StringComparer.OrdinalIgnoreCase);
        return AiStudioLooks.All
            .Where(l => l.IsActive) // the platform's switch wins
            .Select(l => byLook.TryGetValue(l.Key, out var p) ? AiStudioLooks.Merge(l, p) : l)
            .ToList();
    }

    /// <summary>One look as the account sees it: platform overlay + the account's row (prompt/provider/credits come from the platform).</summary>
    private async Task<AiStudioLooks.LookDef?> ResolveAccountLookAsync(int accountId, string key, CancellationToken cancelToken)
    {
        var look = AiStudioLooks.Find(key);
        if (look == null)
            return null;
        var row = await _storage.GetAccountPresetAsync(accountId, look.Key, cancelToken).ConfigureAwait(false);
        return row == null ? look : AiStudioLooks.Merge(look, row);
    }

    //*************************    Account presets ("המראים שלי")    *************************//

    public async Task<IApiResponse<List<AiAccountLookRes>>> GetAccountLooksAsync(AiStudioScopeReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<AiAccountLookRes>>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        await EnsurePresetsAppliedAsync(cancelToken).ConfigureAwait(false);
        var rows = (await _storage.GetAccountPresetsAsync(scope.AccountId, cancelToken).ConfigureAwait(false)).ToDictionary(r => r.Look, StringComparer.OrdinalIgnoreCase);
        response.Data = AiStudioLooks.All.Select(l =>
        {
            rows.TryGetValue(l.Key, out var row);
            var platformRefs = l.References ?? (_options.ReferenceUrls.TryGetValue(l.Key, out var cfg) ? cfg : null) ?? new List<string>();
            return new AiAccountLookRes
            {
                Key = l.Key, Label = l.Label, Description = l.Description, Credits = l.Credits, NeedsSource = l.NeedsSource, IsBackground = AiStudioLooks.IsBackground(l.Key),
                PlatformActive = l.IsActive,
                IsActive = row?.IsActive ?? true,
                StyleNotes = row?.StyleNotes,
                ReferenceUrls = AiStudioLooks.ParseReferenceUrls(row?.ReferenceUrls)?.ToList() ?? new List<string>(),
                PlatformReferenceUrls = platformRefs.ToList(),
                HasOverride = row != null,
            };
        }).ToList();
        return response;
    }

    public async Task<IApiResponse<List<AiAccountLookRes>>> SaveAccountLookAsync(AiAccountLookSaveReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<AiAccountLookRes>>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        await EnsurePresetsAppliedAsync(cancelToken).ConfigureAwait(false);
        var look = AiStudioLooks.Find(req.Look);
        if (look == null)
            return CreateResponse(response, StatusCode.InvalidRequest, ErrUnknownLook);
        var refs = (req.ReferenceUrls ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList();
        if (refs.Count > AiStudioLooks.MaxReferences)
            return CreateResponse(response, StatusCode.InvalidRequest, $"עד {AiStudioLooks.MaxReferences} תמונות רפרנס למראה");
        if (refs.Any(u => !IsHttpUrl(u)))
            return CreateResponse(response, StatusCode.InvalidRequest, "כתובת תמונה לא חוקית");
        if (req.StyleNotes is { Length: > 500 })
            return CreateResponse(response, StatusCode.InvalidRequest, "הערות סגנון: עד 500 תווים");
        await _storage.SaveAccountPresetAsync(scope.AccountId, look.Key, req.IsActive, req.StyleNotes, AiStudioLooks.SerializeReferenceUrls(refs), AuthUser.Id > 0 ? AuthUser.Id : null, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio: account {AccountId} saved its preset of {Look} (active {Active}, notes {HasNotes}, refs {Refs})", scope.AccountId, look.Key, req.IsActive, !string.IsNullOrWhiteSpace(req.StyleNotes), refs.Count);
        return await GetAccountLooksAsync(req, cancelToken).ConfigureAwait(false);
    }

    /// <summary>The worker applies the presets every few minutes; the API process does it on its first status call so a
    /// freshly deployed server does not show the code defaults for its first 30 seconds.</summary>
    private async Task EnsurePresetsAppliedAsync(CancellationToken cancelToken)
    {
        if (AiStudioLooks.Applied)
            return;
        try
        {
            AiStudioLooks.Apply(await _storage.GetPresetsAsync(cancelToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            // Table missing (script not run yet) → code defaults, and keep going.
            _logger.LogWarning(ex, "AiStudio: presets could not be loaded, using code defaults");
        }
    }

    /// <summary>Site.AiDefaultLook / AiImageRatio / AiFinalSize of the site in scope (or the account's first site), validated
    /// against the active looks so the UI never preselects a look that was switched off.</summary>
    private async Task<AiStudioDefaultsRes> ResolveDefaultsAsync(Scope scope, CancellationToken cancelToken)
    {
        var site = await ResolveSiteAsync(scope, cancelToken).ConfigureAwait(false);
        var sourceLooks = AiStudioLooks.Active.Where(l => l.NeedsSource).ToList();
        var explicitLook = sourceLooks.FirstOrDefault(l => string.Equals(l.Key, site?.AiDefaultLook, StringComparison.OrdinalIgnoreCase));
        var look = explicitLook ?? sourceLooks.FirstOrDefault();
        return new AiStudioDefaultsRes
        {
            SiteId = site?.Id,
            Look = look?.Key ?? AiStudioLooks.StudioWhite,
            LookExplicit = explicitLook != null,
            Ratio = NormalizeRatio(site?.AiImageRatio),
            FinalSize = string.Equals(site?.AiFinalSize, "4k", StringComparison.OrdinalIgnoreCase) ? "4k" : "2k",
            TextTone = (site?.AiTextTone ?? string.Empty).Trim().ToLowerInvariant() is "neutral" or "premium" or "playful" ? site!.AiTextTone!.Trim().ToLowerInvariant() : "warm",
            TextLanguage = (site?.AiTextLanguage ?? string.Empty).Trim().ToLowerInvariant() is "en" or "ar" or "ru" ? site!.AiTextLanguage!.Trim().ToLowerInvariant() : "he",
        };
    }

    private async Task<Site?> ResolveSiteAsync(Scope scope, CancellationToken cancelToken)
    {
        if (scope.SiteId is > 0)
            return await _siteStorage.GetSiteAsync(scope.SiteId.Value, cancelToken).ConfigureAwait(false);
        var sites = await _siteStorage.GetSitesByAccountAsync(scope.AccountId, cancelToken).ConfigureAwait(false);
        return sites.FirstOrDefault();
    }

    //*************************    Jobs    *************************//

    public async Task<IApiResponse<AiJobRes>> CreateJobAsync(AiJobCreateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiJobRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);

        await EnsurePresetsAppliedAsync(cancelToken).ConfigureAwait(false);
        var look = AiStudioLooks.Find(req.Look);
        if (look == null || !look.IsActive)
            return CreateResponse(response, StatusCode.InvalidRequest, ErrUnknownLook);
        // The account's own row (active switch, style notes, references) over the platform's.
        var accountPreset = await _storage.GetAccountPresetAsync(scope.AccountId, look.Key, cancelToken).ConfigureAwait(false);
        if (accountPreset != null)
        {
            look = AiStudioLooks.Merge(look, accountPreset);
            if (!look.IsActive)
                return CreateResponse(response, StatusCode.InvalidRequest, ErrLookOff);
        }
        var sourceUrl = req.SourceUrl?.Trim();
        if (look.NeedsSource && !IsHttpUrl(sourceUrl))
            return CreateResponse(response, StatusCode.InvalidRequest, ErrNoSource);
        if (!_providers.Any(p => p.IsConfigured))
            return CreateResponse(response, StatusCode.InvalidOperation, ErrNoProvider);

        var cap = scope.Account.AiStudioDailyCap ?? _options.DefaultDailyCap;
        var usedToday = await _storage.CountJobsSinceAsync(scope.AccountId, DateTime.UtcNow.Date, cancelToken).ConfigureAwait(false);
        if (usedToday >= cap)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrDailyCap);

        var isGenerate = look.Key == AiStudioLooks.Generate;
        if (isGenerate && !string.IsNullOrWhiteSpace(req.Background) && !AiStudioLooks.IsBackground(req.Background))
            return CreateResponse(response, StatusCode.InvalidRequest, ErrUnknownLook);
        if (isGenerate && string.IsNullOrWhiteSpace(req.ProductName) && string.IsNullOrWhiteSpace(StripHtml(req.Description)))
            return CreateResponse(response, StatusCode.InvalidRequest, ErrNoText);

        // Ratio and 4K fall back to the site's defaults (Settings › סטודיו AI); the provider to the look's preset.
        var defaults = await ResolveDefaultsAsync(scope, cancelToken).ConfigureAwait(false);
        var hd = req.Hd ?? defaults.FinalSize == "4k";
        var hdCredits = hd ? Math.Max(0, _options.HdCredits) : 0;
        var provider = scope.Unrestricted && !string.IsNullOrWhiteSpace(req.Provider) ? req.Provider.Trim().ToLowerInvariant() : look.Provider;
        var options = new JobOptions
        {
            Ratio = string.IsNullOrWhiteSpace(req.Ratio) ? defaults.Ratio : NormalizeRatio(req.Ratio),
            Hd = hd,
            ProductName = Trunc(req.ProductName, 300),
            CategoryName = Trunc(req.CategoryName, 200),
            Description = Trunc(StripHtml(req.Description), 1000),
            Scene = Trunc(req.Scene, 500),
            Provider = provider,
            Background = isGenerate ? (AiStudioLooks.IsBackground(req.Background) ? req.Background!.Trim().ToLowerInvariant() : AiStudioLooks.StudioWhite) : null,
            AttachToProduct = isGenerate && req.AttachToProduct == true && req.ProductId is > 0,
        };

        var mode = ModeOf(scope.Account);
        var paid = mode == Account.AiStudioModePaid;
        // George's rule (31.10.2026): an image the shop already had is never on the platform's dime. Only a photo the user just
        // took (quick product, verified against the Media row) and generate-from-text follow the account's mode.
        var fromExisting = look.NeedsSource && _options.ExistingImageAlwaysPaid && !await IsFreshCaptureAsync(scope.AccountId, req.Capture, req.SourceMediaId, cancelToken).ConfigureAwait(false);
        var charge = paid || fromExisting;
        // Community mode is free but every result is donated: the shop must have accepted the pool terms once (spec §7).
        if (!charge && scope.Account.AiStudioPoolConsentAt == null)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrConsentRequired);
        AiCreditLedger? debit = null;
        var price = look.Credits + hdCredits;
        if (charge && price > 0)
        {
            var balance = await _credits.GetBalanceAsync(scope.AccountId, cancelToken).ConfigureAwait(false);
            if (balance < price)
                return CreateResponse(response, StatusCode.InvalidOperation, paid ? ErrNoCredits : ErrNoCreditsExisting);
            // The ledger entry is written BEFORE the job so a worker can never claim a job whose debit is still in flight.
            debit = await _credits.AddLedgerEntryAsync(new AiCreditLedger
            {
                AccountId = scope.AccountId, EntryType = AiCreditLedger.TypeConsumption, Amount = -price,
                Note = look.Label + (hd ? " · 4K" : string.Empty) + (options.ProductName != null ? " · " + options.ProductName : string.Empty), CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
            }, cancelToken).ConfigureAwait(false);
        }
        var job = new AiImageJob
        {
            LedgerEntryId = debit?.Id,
            GuidId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            AccountId = scope.AccountId,
            SiteId = scope.SiteId,
            UserId = AuthUser.Id,
            ProductId = req.ProductId is > 0 ? req.ProductId : null,
            SourceMediaId = req.SourceMediaId is > 0 ? req.SourceMediaId : null,
            SourceUrl = look.NeedsSource ? sourceUrl : null,
            Look = look.Key,
            Status = AiImageJob.StatusQueued,
            OptionsJson = JsonSerializer.Serialize(options),
            Credits = charge ? price : 0,
            // A result the shop paid for is the shop's; only a free community job is donated.
            IsShared = !charge && mode == Account.AiStudioModeCommunity && req.Share != false,
        };
        job = await _storage.AddJobAsync(job, cancelToken).ConfigureAwait(false);
        if (debit != null)
            await _credits.SetLedgerRefJobAsync(debit.Id, job.Id, cancelToken).ConfigureAwait(false);
        _logger.LogInformation("AiStudio job {JobId} queued: account {AccountId}, look {Look}, user {UserId}, charged {Credits} (existing image: {Existing})", job.GuidId, job.AccountId, job.Look, job.UserId, job.Credits, fromExisting);

        response.Data = ToRes(job);
        return response;
    }

    /// <summary>A "capture" claim holds when the source Media row was uploaded by this user, into this account, in the last
    /// AiStudio:FreshCaptureMinutes. A gallery image, a stock image or someone else's upload is an existing image.</summary>
    private async Task<bool> IsFreshCaptureAsync(int accountId, bool? capture, int? sourceMediaId, CancellationToken cancelToken)
    {
        if (capture != true || sourceMediaId is not > 0)
            return false;
        try
        {
            var media = await _mediaStorage.GetMediaAsync(sourceMediaId.Value, cancelToken).ConfigureAwait(false);
            if (media == null || media.IsDeleted || media.AccountId != accountId)
                return false;
            if (media.Origin != null && media.Origin != Media.OriginUpload)
                return false; // an AI result re-fed as a source is an existing image
            if (media.CreationUserId != null && AuthUser.Id > 0 && media.CreationUserId != AuthUser.Id)
                return false;
            return media.CreationTime >= DateTime.UtcNow.AddMinutes(-Math.Max(1, _options.FreshCaptureMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio: could not verify capture media {MediaId}", sourceMediaId);
            return false;
        }
    }

    //*************************    Products without an image    *************************//

    private const int MaxProductsPerBatch = 50;

    /// <summary>"צור תמונות למוצרים ללא תמונה": one generate job per product, attached to the product when the final lands. Charged like
    /// any generate job (account mode); the whole batch is checked against the balance first so it does not stop halfway.</summary>
    public async Task<IApiResponse<AiJobsForProductsRes>> CreateJobsForProductsAsync(AiJobsForProductsReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiJobsForProductsRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        await EnsurePresetsAppliedAsync(cancelToken).ConfigureAwait(false);
        var ids = (req.ProductIds ?? new List<int>()).Where(i => i > 0).Distinct().Take(MaxProductsPerBatch).ToList();
        if (ids.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "לא נבחרו מוצרים");
        var generate = await ResolveAccountLookAsync(scope.AccountId, AiStudioLooks.Generate, cancelToken).ConfigureAwait(false);
        if (generate == null || !generate.IsActive)
            return CreateResponse(response, StatusCode.InvalidRequest, ErrUnknownLook);
        var background = AiStudioLooks.IsBackground(req.Background) ? req.Background!.Trim().ToLowerInvariant() : AiStudioLooks.StudioWhite;

        var products = await _productStorage.GetProductsForAiAsync(scope.AccountId, ids, cancelToken).ConfigureAwait(false);
        var candidates = products.Where(p => p.ProductImage.Count == 0 && !string.IsNullOrWhiteSpace(p.Name)).ToList();
        var skipped = ids.Count - candidates.Count;

        var cap = scope.Account.AiStudioDailyCap ?? _options.DefaultDailyCap;
        var usedToday = await _storage.CountJobsSinceAsync(scope.AccountId, DateTime.UtcNow.Date, cancelToken).ConfigureAwait(false);
        var room = Math.Max(0, cap - usedToday);
        if (room == 0)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrDailyCap);
        if (candidates.Count > room)
        {
            skipped += candidates.Count - room;
            candidates = candidates.Take(room).ToList();
        }
        if (candidates.Count == 0)
        {
            response.Data = new AiJobsForProductsRes { Skipped = skipped, SkippedReason = "לכל המוצרים שנבחרו כבר יש תמונה" };
            return response;
        }

        var defaults = await ResolveDefaultsAsync(scope, cancelToken).ConfigureAwait(false);
        var hd = req.Hd ?? defaults.FinalSize == "4k";
        var paid = ModeOf(scope.Account) == Account.AiStudioModePaid;
        var perJob = generate.Credits + (hd ? Math.Max(0, _options.HdCredits) : 0);
        if (paid && perJob > 0)
        {
            var balance = await _credits.GetBalanceAsync(scope.AccountId, cancelToken).ConfigureAwait(false);
            if (balance < perJob * candidates.Count)
                return CreateResponse(response, StatusCode.InvalidOperation, $"ל-{candidates.Count} תמונות דרושים {perJob * candidates.Count} קרדיטים, ובחשבון יש {balance}. אפשר לרכוש חבילה בהגדרות › סטודיו AI.");
        }

        var result = new AiJobsForProductsRes { Skipped = skipped, SkippedReason = skipped > 0 ? "מוצרים שכבר יש להם תמונה (או מעבר למכסה היומית) לא נוספו" : null };
        foreach (var p in candidates)
        {
            var category = p.ProductCategory.OrderByDescending(pc => pc.IsPrimary).Select(pc => pc.Category?.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            var one = await CreateJobAsync(new AiJobCreateReq
            {
                AccountId = scope.AccountId, SiteId = scope.SiteId,
                Look = AiStudioLooks.Generate, Background = background, Hd = hd,
                ProductId = p.Id, ProductName = p.Name, CategoryName = category, Description = p.ShortDescription,
                AttachToProduct = true,
            }, cancelToken).ConfigureAwait(false);
            if (one.IsSuccessful && one.Data != null)
            {
                result.Jobs.Add(one.Data);
                result.CreditsCharged += one.Data.Credits;
            }
            else
            {
                // One product failing (e.g. the cap was reached in between) must not undo the ones already queued.
                result.Skipped++;
                result.SkippedReason = one.Description ?? result.SkippedReason;
                _logger.LogWarning("AiStudio: products batch, product {ProductId} not queued: {Error}", p.Id, one.Description);
            }
        }
        response.Data = result;
        return response;
    }

    public async Task<IApiResponse<AiJobRes>> GetJobAsync(Guid id, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiJobRes>();
        var (job, status, error) = await LoadOwnedJobAsync(id, accountId, cancelToken).ConfigureAwait(false);
        if (job == null)
            return CreateResponse(response, status, error!);
        response.Data = ToRes(job);
        return response;
    }

    public async Task<IApiResponse<List<AiJobRes>>> ListJobsAsync(AiJobListReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<AiJobRes>>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        var jobs = await _storage.GetRecentJobsAsync(scope.AccountId, req.ProductId, null, req.Take, cancelToken).ConfigureAwait(false);
        response.Data = jobs.Select(ToRes).ToList();
        return response;
    }

    public async Task<IApiResponse<AiJobRes>> CancelJobAsync(Guid id, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiJobRes>();
        var (job, status, error) = await LoadOwnedJobAsync(id, accountId, cancelToken).ConfigureAwait(false);
        if (job == null)
            return CreateResponse(response, status, error!);
        var canceled = await _storage.CancelAsync(job.Id, cancelToken).ConfigureAwait(false);
        // Nothing was delivered yet: the credits go back. A job canceled after its preview keeps the charge.
        if (canceled && string.IsNullOrEmpty(job.PreviewUrl))
            await _credits.RefundJobAsync(job.Id, "בוטל לפני שנוצרה תמונה", cancelToken).ConfigureAwait(false);
        job = await _storage.GetJobAsync(job.Id, cancelToken).ConfigureAwait(false) ?? job;
        response.Data = ToRes(job);
        await _notifier.JobUpdatedAsync(job.AccountId, response.Data, cancelToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>Same photo, same look, new dice. The first retry of a job is free (spec §7); later ones cost the look again.</summary>
    public async Task<IApiResponse<AiJobRes>> RetryJobAsync(Guid id, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiJobRes>();
        var (job, status, error) = await LoadOwnedJobAsync(id, accountId, cancelToken).ConfigureAwait(false);
        if (job == null)
            return CreateResponse(response, status, error!);

        var root = job.RetryOfJobId ?? job.Id;
        var retries = await _storage.CountRetriesAsync(root, cancelToken).ConfigureAwait(false);
        var retryCredits = retries == 0 ? 0 : job.Credits;
        AiCreditLedger? retryDebit = null;
        if (retryCredits > 0)
        {
            var balance = await _credits.GetBalanceAsync(job.AccountId, cancelToken).ConfigureAwait(false);
            if (balance < retryCredits)
                return CreateResponse(response, StatusCode.InvalidOperation, ErrNoCredits);
            retryDebit = await _credits.AddLedgerEntryAsync(new AiCreditLedger
            {
                AccountId = job.AccountId, EntryType = AiCreditLedger.TypeConsumption, Amount = -retryCredits,
                Note = (AiStudioLooks.Find(job.Look)?.Label ?? job.Look) + " · נסה שוב", CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
            }, cancelToken).ConfigureAwait(false);
        }
        var copy = new AiImageJob
        {
            LedgerEntryId = retryDebit?.Id,
            GuidId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow,
            AccountId = job.AccountId,
            SiteId = job.SiteId,
            UserId = AuthUser.Id,
            ProductId = job.ProductId,
            SourceMediaId = job.SourceMediaId,
            SourceUrl = job.SourceUrl,
            Look = job.Look,
            Status = AiImageJob.StatusQueued,
            OptionsJson = job.OptionsJson,
            IncidentalText = job.IncidentalText, // identification is reused: same photo
            Credits = retryCredits,
            RetryOfJobId = root,
            IsShared = job.IsShared,
        };
        copy = await _storage.AddJobAsync(copy, cancelToken).ConfigureAwait(false);
        if (retryDebit != null)
            await _credits.SetLedgerRefJobAsync(retryDebit.Id, copy.Id, cancelToken).ConfigureAwait(false);
        response.Data = ToRes(copy);
        return response;
    }

    public async Task<IApiResponse<bool>> RateJobAsync(Guid id, AiJobRateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<bool>();
        var (job, status, error) = await LoadOwnedJobAsync(id, req.AccountId, cancelToken).ConfigureAwait(false);
        if (job == null)
            return CreateResponse(response, status, error!);
        response.Data = await _storage.RateAsync(job.Id, req.Rating is 0 or 1 ? req.Rating : null, cancelToken).ConfigureAwait(false);
        return response;
    }

    //*************************    Shared helpers    *************************//

    /// <summary>Options the API stored for the runner (serialised in AiImageJob.OptionsJson).</summary>
    //*************************    Suggest (text from photo)    *************************//

    private const string ErrNoVision = "זיהוי טקסט מתמונה אינו מוגדר בשרת (AiStudio:Anthropic)";
    private const string ErrSuggestFailed = "לא הצלחנו לקרוא את המוצר מהתמונה. אפשר למלא את הפרטים ידנית.";
    /// <summary>Spec §7: "טקסט מהתמונה" = 1 credit (no ledger yet; reported so the UI can show the price).</summary>
    private const int SuggestCredits = 1;

    /// <summary>
    /// "✨ כתוב ב-AI" (spec §6.1 step 4): Claude reads the uploaded photo and returns name, descriptions, category (one of the
    /// shop's), brand and whether the product is sold by weight with typical weights. Synchronous, 5-8 s; counts toward the
    /// daily cap like an image job so the free community mode cannot be farmed for copywriting.
    /// </summary>
    public async Task<IApiResponse<AiSuggestRes>> SuggestAsync(AiSuggestReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiSuggestRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        if (!_vision.IsConfigured)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrNoVision);
        var sourceUrl = req.SourceUrl?.Trim();
        if (!IsHttpUrl(sourceUrl))
            return CreateResponse(response, StatusCode.InvalidRequest, ErrNoSource);

        var cap = scope.Account.AiStudioDailyCap ?? _options.DefaultDailyCap;
        var usedToday = await _storage.CountJobsSinceAsync(scope.AccountId, DateTime.UtcNow.Date, cancelToken).ConfigureAwait(false);
        if (usedToday >= cap)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrDailyCap);
        var suggestPaid = ModeOf(scope.Account) == Account.AiStudioModePaid;
        // A real retail barcode (EAN-8/13, UPC) is worth a web lookup; a 2-5 digit PLU is the shop's own code and is not.
        var barcode = Trunc(req.Barcode, 40);
        var digits = barcode == null ? string.Empty : new string(barcode.Where(char.IsDigit).ToArray());
        var webSearch = req.WebSearch != false && digits.Length >= 8 && digits.Length == barcode!.Trim().Length && _vision.WebSearchAvailable;
        var suggestPrice = webSearch ? Math.Max(SuggestCredits, _options.WebSearchCredits) : SuggestCredits;
        if (suggestPaid && await _credits.GetBalanceAsync(scope.AccountId, cancelToken).ConfigureAwait(false) < suggestPrice)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrNoCredits);
        var suggestDefaults = await ResolveDefaultsAsync(scope, cancelToken).ConfigureAwait(false);

        byte[] raw;
        try
        {
            var client = _httpClientFactory.CreateClient(AiStudioJobRunner.HttpClientName);
            using var res = await client.GetAsync(sourceUrl, cancelToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return CreateResponse(response, StatusCode.InvalidRequest, $"לא ניתן לקרוא את התמונה (HTTP {(int)res.StatusCode})");
            raw = await res.Content.ReadAsByteArrayAsync(cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio suggest: download failed for {Url}", sourceUrl);
            return CreateResponse(response, StatusCode.InvalidRequest, "לא ניתן לקרוא את התמונה");
        }
        if (raw.Length == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "התמונה ריקה");

        ImageInput photo;
        try
        {
            photo = ImagePostProcess.PrepareSource(raw, 1536, "photo.jpg");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio suggest: not an image ({Url})", sourceUrl);
            return CreateResponse(response, StatusCode.InvalidRequest, "הקובץ אינו תמונה");
        }

        var categories = (req.Categories ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Take(300).ToList();
        var copy = await _vision.DescribeAsync(photo, Trunc(req.ProductName, 200), Trunc(req.CategoryName, 200), categories, barcode, suggestDefaults.TextTone, suggestDefaults.TextLanguage, webSearch, cancelToken).ConfigureAwait(false);
        if (copy == null)
            return CreateResponse(response, StatusCode.InvalidOperation, ErrSuggestFailed);
        if (!copy.Usable)
            return CreateResponse(response, StatusCode.InvalidRequest, string.IsNullOrWhiteSpace(copy.UnusableReason) ? "בתמונה לא רואים מוצר שלם. צלמו שוב מקרוב." : copy.UnusableReason);

        _logger.LogInformation("AiStudio suggest: account {AccountId} user {UserId} -> \"{Name}\" ({Tokens} tokens, web searches {Searches})", scope.AccountId, AuthUser.Id, copy.Name, (copy.InputTokens ?? 0) + (copy.OutputTokens ?? 0), copy.WebSearches);
        var describeCost = _vision.CostUsd(copy.InputTokens, copy.OutputTokens);
        if (copy.WebSearches > 0)
            describeCost = (describeCost ?? 0m) + copy.WebSearches * _options.WebSearchUsdPerSearch;
        if (describeCost is > 0m)
        {
            await _storage.AddSpendAsync(new AiProviderSpend
            {
                Provider = AiProviderSpend.ProviderAnthropic, Model = _vision.ModelName, Kind = AiProviderSpend.KindDescribe, AccountId = scope.AccountId,
                InputTokens = copy.InputTokens, OutputTokens = copy.OutputTokens, AmountUsd = describeCost.Value,
            }, cancelToken).ConfigureAwait(false);
        }
        // A web lookup that found nothing is billed as a plain text call.
        var charged = webSearch && copy.FoundOnWeb ? suggestPrice : SuggestCredits;
        if (suggestPaid)
        {
            await _credits.AddLedgerEntryAsync(new AiCreditLedger
            {
                AccountId = scope.AccountId, EntryType = AiCreditLedger.TypeConsumption, Amount = -charged,
                Note = (charged > SuggestCredits ? "טקסט + חיפוש ברשת" : "טקסט מהתמונה") + (copy.Name != null ? " · " + copy.Name : string.Empty), CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
            }, cancelToken).ConfigureAwait(false);
        }
        response.Data = new AiSuggestRes
        {
            Name = copy.Name,
            ShortDescription = copy.ShortDescription,
            LongDescription = copy.LongDescription,
            CategoryName = copy.CategoryName,
            BrandName = copy.BrandName,
            IsPackaged = copy.IsPackaged,
            IsWeighted = copy.IsWeighted,
            WeightOptionsKg = copy.WeightOptionsKg,
            UnitWeightKg = copy.UnitWeightKg,
            Credits = suggestPaid ? charged : 0,
            WebSearched = webSearch,
            FoundOnWeb = copy.FoundOnWeb,
            WebSource = copy.WebSource,
        };
        return response;
    }

    //*************************    Community pool consent    *************************//

    /// <summary>The shop (account admin) accepted the community-pool terms. Idempotent; the stamp is kept on the account.</summary>
    public async Task<IApiResponse<AiStudioStatusRes>> SetPoolConsentAsync(AiStudioScopeReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<AiStudioStatusRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteId, requireEnabled: true, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return CreateResponse(response, status, error!);
        if (scope.Account.AiStudioPoolConsentAt == null)
        {
            await _credits.SetPoolConsentAsync(scope.AccountId, DateTime.UtcNow, cancelToken).ConfigureAwait(false);
            _logger.LogInformation("AiStudio: account {AccountId} accepted the community pool terms (user {UserId})", scope.AccountId, AuthUser.Id);
        }
        return await GetStatusAsync(req, cancelToken).ConfigureAwait(false);
    }

    public sealed class JobOptions
    {
        public string Ratio { get; set; } = "1:1";
        /// <summary>Render the final at 4K (AiStudio:HdPx / Gemini 4K).</summary>
        public bool Hd { get; set; }
        public string? ProductName { get; set; }
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public string? Scene { get; set; }
        public string? Provider { get; set; }
        /// <summary><c>generate</c>: the background look (studio_white | dark | marble | ambience).</summary>
        public string? Background { get; set; }
        /// <summary>Filled by the runner after Claude identified the product.</summary>
        public string? IdentifiedName { get; set; }
        public string? CategoryEn { get; set; }
        public bool? IsPackaged { get; set; }
        /// <summary><c>generate</c>: the visual brief Claude wrote (kept for the job log and retries).</summary>
        public string? Brief { get; set; }
        /// <summary><c>generate</c> for a product without an image: the runner sets the final as the product's image.</summary>
        public bool AttachToProduct { get; set; }
    }

    public static JobOptions ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JobOptions();
        try
        {
            return JsonSerializer.Deserialize<JobOptions>(json) ?? new JobOptions();
        }
        catch
        {
            return new JobOptions();
        }
    }

    public static AiJobRes ToRes(AiImageJob j)
    {
        var options = ParseOptions(j.OptionsJson);
        return new AiJobRes
        {
            Id = j.GuidId,
            CreationTime = j.CreationTime,
            AccountId = j.AccountId,
            SiteId = j.SiteId,
            ProductId = j.ProductId,
            Look = j.Look,
            LookLabel = AiStudioLooks.Find(j.Look)?.Label,
            Background = options.Background,
            BackgroundLabel = options.Background == null ? null : AiStudioLooks.Find(options.Background)?.Label,
            Status = j.Status,
            Stage = j.Stage,
            Error = j.Error,
            SourceUrl = j.SourceUrl,
            PreviewUrl = j.PreviewUrl,
            ResultUrl = j.ResultUrl,
            ResultMediaId = j.ResultMediaId,
            Credits = j.Credits,
            Refunded = j.Refunded,
            IsRetry = j.RetryOfJobId != null,
            FidelityScore = j.FidelityScore,
            AestheticScore = j.AestheticScore,
            JudgeVerdict = j.JudgeVerdict,
            JudgeIssues = string.IsNullOrWhiteSpace(j.JudgeIssues) ? new List<string>() : SafeList(j.JudgeIssues),
            UserRating = j.UserRating,
            IsShared = j.IsShared,
            PreviewMs = j.PreviewMs,
            FinalMs = j.FinalMs,
            Provider = j.Provider,
            IdentifiedName = options.IdentifiedName,
            Hd = options.Hd,
        };
    }

    private static List<string> SafeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string> { json }; }
    }

    private static string ModeOf(Account a) =>
        string.Equals(a.AiStudioMode, Account.AiStudioModePaid, StringComparison.OrdinalIgnoreCase) ? Account.AiStudioModePaid : Account.AiStudioModeCommunity;

    private static string NormalizeRatio(string? r) => (r ?? "1:1").Trim() switch
    {
        "4:5" => "4:5",
        "3:4" => "3:4",
        "4:3" => "4:3",
        "16:9" => "16:9",
        _ => "1:1",
    };

    private static bool IsHttpUrl(string? s) =>
        !string.IsNullOrWhiteSpace(s) && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static string? Trunc(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];

    /// <summary>The product form's descriptions are Quill HTML; Claude and the image model want plain text.</summary>
    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<(br|/p|/div|/li)\\s*/?>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
        return text.Length == 0 ? null : text;
    }

    //*************************    Scope    *************************//

    private sealed class Scope
    {
        public int AccountId { get; init; }
        public int? SiteId { get; init; }
        public Account Account { get; init; } = null!;
        public bool Unrestricted { get; init; }
    }

    private async Task<(Scope? Scope, StatusCode Status, string? Error)> ResolveScopeAsync(int? requestedAccountId, int? requestedSiteId, bool requireEnabled, CancellationToken cancelToken)
    {
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        var unrestricted = AuthUser.IsMaster || user?.RoleId == (int)UserRole.Admin;
        var accountId = unrestricted && requestedAccountId is > 0 ? requestedAccountId : user?.AccountId;
        if (accountId is not > 0)
            return (null, StatusCode.InvalidRequest, ErrNoAccount);

        var account = await _accountStorage.GetAccountAsync(accountId.Value, cancelToken).ConfigureAwait(false);
        if (account == null)
            return (null, StatusCode.ItemNotFound, ErrNoAccount);
        if (requireEnabled && !account.AiStudioEnabled)
            return (null, StatusCode.UnauthorizedData, ErrDisabled);

        int? siteId = null;
        if (requestedSiteId is > 0)
        {
            var ok = await _siteAccess.CanAccessSiteAsync(AuthUser.Id, AuthUser.IsMaster, requestedSiteId.Value, unrestricted ? accountId : null, cancelToken).ConfigureAwait(false);
            if (!ok)
                return (null, StatusCode.UnauthorizedData, "אין גישה לסניף המבוקש");
            siteId = requestedSiteId;
        }

        return (new Scope { AccountId = accountId.Value, SiteId = siteId, Account = account, Unrestricted = unrestricted }, StatusCode.Ok, null);
    }

    private async Task<(AiImageJob? Job, StatusCode Status, string? Error)> LoadOwnedJobAsync(Guid id, int? requestedAccountId, CancellationToken cancelToken)
    {
        var job = await _storage.GetJobAsync(id, cancelToken).ConfigureAwait(false);
        if (job == null)
            return (null, StatusCode.ItemNotFound, ErrNoJob);
        var (scope, status, error) = await ResolveScopeAsync(requestedAccountId ?? job.AccountId, null, requireEnabled: false, cancelToken).ConfigureAwait(false);
        if (scope == null)
            return (null, status, error);
        if (scope.AccountId != job.AccountId && !scope.Unrestricted)
            return (null, StatusCode.UnauthorizedData, ErrNoJob);
        return (job, StatusCode.Ok, null);
    }
}
