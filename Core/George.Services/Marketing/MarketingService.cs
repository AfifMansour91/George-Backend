using System.Text.Json;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Request;
using George.Services.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Marketing module API (phases 0-2): segments, the send wizard (estimate / test / create), results, quota and
/// settings - plus the two public token endpoints (tracked link, unsubscribe). Actual sending is done by
/// <see cref="MarketingDispatchService"/>; creating a send here only stores its definition.
/// </summary>
public class MarketingService : ServiceBase
{
    public const string ErrMarketingDisabled = "מודול השיווק אינו פעיל בחשבון זה";
    private const string ErrNoAccount = "לא נמצא חשבון לפעולה";
    private const string ErrNoSites = "אין סניפים זמינים לשליחה";
    public const int MaxPinnedSegments = 5;
    private const int MaxAttributionWindowHours = 168;
    private const string SampleCustomerName = "דנה";
    private const string SampleToken = "xxxxx";
    /// <summary>Token placed in "שלח לעצמי לבדיקה" messages; the public pages recognise it and explain instead of failing.</summary>
    public const string TestToken = "tst00";

    private readonly MarketingStorage _storage;
    private readonly UserStorage _userStorage;
    private readonly AccountStorage _accountStorage;
    private readonly SiteAccessService _siteAccess;
    private readonly AccountSmsService _accountSms;
    private readonly InforuQuotaService _inforuQuota;
    private readonly IConfiguration _configuration;

    public MarketingService(
        ILogger<MarketingService> logger,
        IMapper mapper,
        CacheManager cache,
        MarketingStorage storage,
        UserStorage userStorage,
        AccountStorage accountStorage,
        SiteAccessService siteAccess,
        AccountSmsService accountSms,
        InforuQuotaService inforuQuota,
        IConfiguration configuration)
        : base(logger, mapper, cache)
    {
        _inforuQuota = inforuQuota;
        _storage = storage;
        _userStorage = userStorage;
        _accountStorage = accountStorage;
        _siteAccess = siteAccess;
        _accountSms = accountSms;
        _configuration = configuration;
    }

    /// <summary>Base of the short links in a message. A dedicated short domain later; the API's public URL until then.</summary>
    public static string ShortLinkBaseUrl(IConfiguration configuration)
    {
        var url = configuration["Marketing:ShortLinkBaseUrl"];
        if (string.IsNullOrWhiteSpace(url))
            url = configuration["Payment:PublicApiBaseUrl"];
        return (url ?? string.Empty).Trim().TrimEnd('/');
    }


    //*************************    Scope    *************************//

    private sealed class Scope
    {
        public int AccountId { get; init; }
        public HashSet<int> SiteIds { get; init; } = new();
        public bool Unrestricted { get; init; }
        /// <summary>Master, system admin or account admin - may change account-wide settings and delete shared segments. A site admin only sends and reads.</summary>
        public bool IsAccountAdmin { get; init; }
    }

    public const string ErrAccountAdminOnly = "רק מנהל החשבון יכול לשנות את ההגדרה הזו.";

    /// <summary>
    /// The account and branches this call acts on. The account is the caller's own; only an unrestricted caller
    /// (master / system admin) may name another one. Requested branches are intersected with what the caller may
    /// access - a site manager asking for a cross-branch audience is silently narrowed to their branch (spec §3.3).
    /// </summary>
    private async Task<(Scope? Scope, StatusCode Status, string? Error)> ResolveScopeAsync(int? requestedAccountId, IEnumerable<int>? requestedSiteIds, CancellationToken cancelToken)
    {
        var user = await _userStorage.GetThinUserAsync(AuthUser.Id, cancelToken).ConfigureAwait(false);
        var unrestricted = AuthUser.IsMaster || user?.RoleId == (int)UserRole.Admin;
        var accountId = unrestricted && requestedAccountId is > 0 ? requestedAccountId : user?.AccountId;
        if (accountId is not > 0)
            return (null, StatusCode.InvalidRequest, ErrNoAccount);

        var account = await _accountStorage.GetAccountAsync(accountId.Value, cancelToken).ConfigureAwait(false);
        if (account == null)
            return (null, StatusCode.ItemNotFound, ErrNoAccount);
        if (!account.MarketingEnabled)
            return (null, StatusCode.UnauthorizedData, ErrMarketingDisabled);

        var allowed = await _siteAccess
            .GetAccessibleSiteIdsAsync(AuthUser.Id, AuthUser.IsMaster, unrestricted ? accountId : null, cancelToken)
            .ConfigureAwait(false);
        var requested = requestedSiteIds?.Where(id => id > 0).ToHashSet();
        if (requested is { Count: > 0 })
            allowed.IntersectWith(requested);
        if (allowed.Count == 0)
            return (null, StatusCode.InvalidRequest, ErrNoSites);

        var isAccountAdmin = unrestricted || user?.RoleId == (int)UserRole.AccountAdmin;
        return (new Scope { AccountId = accountId.Value, SiteIds = allowed, Unrestricted = unrestricted, IsAccountAdmin = isAccountAdmin }, StatusCode.Ok, null);
    }

    private async Task<(List<SegmentCondition> Conditions, string? Error)> ResolveAudienceAsync(int accountId, MarketingAudienceReq? audience, CancellationToken cancelToken)
    {
        var type = (audience?.Type ?? "all").Trim().ToLowerInvariant();
        switch (type)
        {
            case "all":
                return (new List<SegmentCondition>(), null);
            case "system":
            {
                var conditions = MarketingSystemSegments.Find(audience!.SystemKey);
                return conditions == null
                    ? (new List<SegmentCondition>(), "סגמנט מוכן לא מוכר")
                    : (conditions.ToList(), null);
            }
            case "segment":
            {
                var segment = audience!.SegmentId is > 0
                    ? await _storage.GetSegmentAsync(accountId, audience.SegmentId.Value, cancelToken).ConfigureAwait(false)
                    : null;
                return segment == null
                    ? (new List<SegmentCondition>(), "הסגמנט לא נמצא")
                    : (MarketingSegmentQuery.Parse(segment.DefinitionJson), null);
            }
            case "filter":
            {
                var conditions = audience!.Conditions ?? new List<SegmentCondition>();
                return (conditions, MarketingSegmentQuery.Validate(conditions));
            }
            case "resend":
            {
                // "שלח שוב למי שלא הזמין" - the earlier send must belong to this account.
                var source = audience!.SourceSendId is > 0 ? await _storage.GetSendAsync(accountId, audience.SourceSendId.Value, cancelToken).ConfigureAwait(false) : null;
                if (source == null)
                    return (new List<SegmentCondition>(), "השליחה המקורית לא נמצאה");
                return (new List<SegmentCondition> { new() { Axis = SegmentAxis.Resend, Operator = SegmentOperator.NonBuyers, Value = source.Id.ToString() } }, null);
            }
            case "customers":
            {
                var ids = (audience!.CustomerIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
                var condition = new SegmentCondition { Axis = SegmentAxis.Customers, Operator = SegmentOperator.Ids, Value = string.Join(",", ids) };
                var error = MarketingSegmentQuery.Validate(new[] { condition }, allowSystemAxes: true);
                return (new List<SegmentCondition> { condition }, error);
            }
            default:
                return (new List<SegmentCondition>(), "סוג קהל לא מוכר");
        }
    }

    private static (DateTime UtcNow, DateTime IsraelToday) Clock()
    {
        var now = DateTime.UtcNow;
        return (now, MarketingSendWindow.ToIsrael(now).Date);
    }


    //*************************    Segments    *************************//

    public async Task<IApiResponse<MarketingSegmentsRes>> GetSegmentsAsync(MarketingScopeReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSegmentsRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var (now, today) = Clock();
        var res = new MarketingSegmentsRes { MaxConditions = MarketingSegmentQuery.MaxConditions, MaxPinned = MaxPinnedSegments };

        (res.AllCustomersCount, res.AllCustomersConsentCount) =
            await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, null, now, today, cancelToken);

        foreach (var (key, conditions) in MarketingSystemSegments.All)
        {
            var (count, consent) = await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, conditions, now, today, cancelToken);
            res.System.Add(new MarketingSegmentRes { SystemKey = key, Conditions = conditions.ToList(), Count = count, ConsentCount = consent });
        }

        foreach (var segment in await _storage.GetSegmentsAsync(scope.AccountId, cancelToken))
        {
            var conditions = MarketingSegmentQuery.Parse(segment.DefinitionJson);
            var (count, consent) = await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, conditions, now, today, cancelToken);
            res.Saved.Add(new MarketingSegmentRes
            {
                Id = segment.Id, Name = segment.Name, Conditions = conditions, IsPinned = segment.IsPinned, Count = count, ConsentCount = consent,
            });
        }

        response.Data = res;
        return response;
    }

    public async Task<IApiResponse<MarketingSegmentPreviewRes>> PreviewSegmentAsync(MarketingSegmentPreviewReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSegmentPreviewRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var (conditions, audienceError) = await ResolveAudienceAsync(scope.AccountId, req.Audience, cancelToken);
        if (audienceError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, audienceError);

        var (now, today) = Clock();
        var query = await _storage.BuildAudienceQueryAsync(scope.AccountId, scope.SiteIds, conditions, now, today, cancelToken);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var byPhone = digits.Length >= 3;
            query = query.Where(r => (r.Customer.Name != null && r.Customer.Name.Contains(term)) || (byPhone && r.Customer.NormalizedPhone.Contains(digits)));
        }
        var count = await query.CountAsync(cancelToken);
        var consent = count == 0 ? 0 : await MarketingSegmentQuery.WhereSmsConsent(query).CountAsync(cancelToken);

        var take = Math.Clamp(req.Take, 1, 100);
        var items = await query
            .OrderByDescending(r => r.LastOrderAt).ThenBy(r => r.Customer.Id)
            .Skip(Math.Max(0, req.Skip)).Take(take)
            .Select(r => new MarketingSegmentCustomerRes
            {
                Id = r.Customer.Id,
                SiteId = r.Customer.SiteId,
                Name = r.Customer.Name,
                Phone = r.Customer.Phone,
                City = r.Customer.City,
                OrderCount = r.OrderCount,
                TotalRevenue = r.TotalRevenue,
                LastOrderAt = r.LastOrderAt,
                HasConsent = r.Customer.MarketingSms && r.Customer.OptedOutAt == null,
            })
            .ToListAsync(cancelToken);

        response.Data = new MarketingSegmentPreviewRes { Count = count, ConsentCount = consent, Items = items };
        return response;
    }

    public async Task<IApiResponse<MarketingSegmentRes>> SaveSegmentAsync(int? segmentId, MarketingSegmentSaveReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSegmentRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var name = (req.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "יש לתת שם לסגמנט");
        if (name.Length > 200)
            name = name.Substring(0, 200);
        if (req.Conditions == null || req.Conditions.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "סגמנט חייב לכלול לפחות תנאי אחד");
        var validation = MarketingSegmentQuery.Validate(req.Conditions);
        if (validation != null)
            return CreateResponse(response, StatusCode.InvalidRequest, validation);

        if (req.IsPinned && await _storage.CountPinnedSegmentsAsync(scope.AccountId, segmentId, cancelToken) >= MaxPinnedSegments)
            return CreateResponse(response, StatusCode.InvalidRequest, $"אפשר להצמיד עד {MaxPinnedSegments} סגמנטים");

        var json = MarketingSegmentQuery.Serialize(req.Conditions);
        MarketingSegment? saved;
        if (segmentId is > 0)
        {
            saved = await _storage.UpdateSegmentAsync(scope.AccountId, segmentId.Value, s =>
            {
                s.Name = name;
                s.DefinitionJson = json;
                s.IsPinned = req.IsPinned;
            }, cancelToken);
            if (saved == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "הסגמנט לא נמצא");
        }
        else
        {
            saved = await _storage.AddSegmentAsync(new MarketingSegment
            {
                AccountId = scope.AccountId,
                Name = name,
                DefinitionJson = json,
                IsPinned = req.IsPinned,
                CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
            }, cancelToken);
        }

        var (now, today) = Clock();
        var (count, consent) = await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, req.Conditions, now, today, cancelToken);
        response.Data = new MarketingSegmentRes
        {
            Id = saved.Id, Name = saved.Name, Conditions = req.Conditions, IsPinned = saved.IsPinned, Count = count, ConsentCount = consent,
        };
        return response;
    }

    public async Task<IApiResponse<bool>> DeleteSegmentAsync(int segmentId, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<bool>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        if (!scope.IsAccountAdmin)
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAccountAdminOnly);
        if (!await _storage.DeleteSegmentAsync(scope.AccountId, segmentId, cancelToken))
            return CreateResponse(response, StatusCode.ItemNotFound, "הסגמנט לא נמצא");
        response.Data = true;
        return response;
    }

    public async Task<IApiResponse<List<string>>> GetCitiesAsync(MarketingScopeReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<List<string>>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        response.Data = await _storage.GetCustomerCitiesAsync(scope.AccountId, scope.SiteIds, cancelToken);
        return response;
    }


    //*************************    Send wizard    *************************//

    public async Task<IApiResponse<MarketingEstimateRes>> EstimateAsync(MarketingEstimateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingEstimateRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var (conditions, audienceError) = await ResolveAudienceAsync(scope.AccountId, req.Audience, cancelToken);
        if (audienceError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, audienceError);

        var (now, today) = Clock();
        var (count, consent) = await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, conditions, now, today, cancelToken);

        var settings = await _storage.GetSettingsAsync(scope.AccountId, cancelToken);
        var preview = await RenderSampleAsync(scope, req.Body, req.LinkUrl, cancelToken);
        var units = SmsUnits.Calculate(preview);
        var quota = await BuildQuotaAsync(scope.AccountId, includeLedger: false, cancelToken);

        var requestedAt = req.ScheduledAt.HasValue && req.ScheduledAt.Value > now ? DateTime.SpecifyKind(req.ScheduledAt.Value, DateTimeKind.Utc) : now;
        var allowedAt = MarketingSendWindow.NextAllowed(requestedAt, settings);
        var (noPhone, duplicates) = consent == 0
            ? (0, 0)
            : await _storage.CountAudiencePhoneIssuesAsync(scope.AccountId, scope.SiteIds, conditions, now, today, cancelToken);
        var smsConfig = await _accountSms.GetAccountConfigAsync(scope.AccountId, cancelToken);
        var senderName = !string.IsNullOrWhiteSpace(smsConfig?.FromName) ? smsConfig!.FromName : SmsProvider.SystemSenderName;

        response.Data = new MarketingEstimateRes
        {
            AudienceCount = count,
            NoConsentCount = count - consent,
            WillSendCount = consent,
            UnitsPerMessage = units,
            TotalUnits = units * consent,
            MessageLength = preview.Length,
            Quota = quota,
            QuotaSufficient = quota.IsExempt || quota.Balance >= units * Math.Max(0, consent - noPhone - duplicates),
            AllowedNow = allowedAt == requestedAt,
            DeferredTo = allowedAt == requestedAt ? null : allowedAt,
            SendWindowStart = settings.SendWindowStart,
            SendWindowEnd = settings.SendWindowEnd,
            BlockShabbatAndHolidays = settings.BlockShabbatAndHolidays,
            FrequencyCapCount = settings.FrequencyCapCount,
            FrequencyCapDays = settings.FrequencyCapDays,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? null : senderName,
            NoPhoneCount = noPhone,
            DuplicateCount = duplicates,
            Preview = preview,
        };
        return response;
    }

    public async Task<IApiResponse<MarketingSendRes>> CreateSendAsync(MarketingSendCreateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSendRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var contentError = ValidateContent(req.Body, req.LinkUrl);
        if (contentError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, contentError);

        var (conditions, audienceError) = await ResolveAudienceAsync(scope.AccountId, req.Audience, cancelToken);
        if (audienceError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, audienceError);

        var config = await _accountSms.GetAccountConfigAsync(scope.AccountId, cancelToken);
        if (!SmsProvider.CanSendWith(config))
            return CreateResponse(response, StatusCode.InvalidRequest, "לא מוגדר חשבון SMS לשליחה");

        var (clockNow, clockToday) = Clock();
        var (_, consentNow) = await _storage.CountSegmentAsync(scope.AccountId, scope.SiteIds, conditions, clockNow, clockToday, cancelToken);
        if (consentNow == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "אין למי לשלוח - אין בקהל לקוחות שאישרו דיוור");

        var settings = await _storage.GetSettingsAsync(scope.AccountId, cancelToken);
        var now = DateTime.UtcNow;
        var requestedAt = req.ScheduledAt.HasValue && req.ScheduledAt.Value > now ? DateTime.SpecifyKind(req.ScheduledAt.Value, DateTimeKind.Utc) : now;
        var allowedAt = MarketingSendWindow.NextAllowed(requestedAt, settings);

        var body = req.Body.Trim();
        var audienceType = (req.Audience?.Type ?? "all").Trim().ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(req.Name) ? DefaultSendName(body) : req.Name.Trim();

        var send = await _storage.AddSendAsync(new MarketingSend
        {
            AccountId = scope.AccountId,
            Type = "manual",
            Name = name.Length > 200 ? name.Substring(0, 200) : name,
            Channel = "sms",
            Status = MarketingStorage.SendStatus.Scheduled,
            AudienceType = audienceType,
            SegmentId = audienceType == "segment" ? req.Audience!.SegmentId : null,
            SystemSegmentKey = audienceType == "system" ? req.Audience!.SystemKey : null,
            AudienceLabel = Truncate(req.AudienceLabel?.Trim(), 300),
            AudienceDefinitionJson = MarketingSegmentQuery.Serialize(conditions),
            SiteIdsJson = JsonSerializer.Serialize(scope.SiteIds.OrderBy(id => id)),
            Body = body,
            LinkUrl = string.IsNullOrWhiteSpace(req.LinkUrl) ? null : req.LinkUrl.Trim(),
            ScheduledAt = allowedAt,
            // Keep the time the shop asked for, so a deferral never looks like the system forgot.
            OriginalScheduledAt = allowedAt == requestedAt ? null : requestedAt,
            AttributionWindowHours = Math.Clamp(settings.AttributionWindowHours, 1, MaxAttributionWindowHours),
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken);

        response.Data = MapSend(send, null);
        return response;
    }

    /// <summary>Spec 6.2: a scheduled send is editable until 10 minutes before it goes out. Same validation as creation; the audience is re-resolved at send time anyway.</summary>
    public const int EditCutoffMinutes = 10;

    public static bool IsEditable(MarketingSend send, DateTime utcNow) =>
        send.Status == MarketingStorage.SendStatus.Scheduled && send.StartedAt == null && send.ScheduledAt > utcNow.AddMinutes(EditCutoffMinutes);

    public async Task<IApiResponse<MarketingSendRes>> UpdateSendAsync(int sendId, MarketingSendCreateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSendRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var existing = await _storage.GetSendAsync(scope.AccountId, sendId, cancelToken);
        if (existing == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "השליחה לא נמצאה");
        var now = DateTime.UtcNow;
        if (!IsEditable(existing, now))
            return CreateResponse(response, StatusCode.InvalidRequest, $"אפשר לערוך שליחה מתוזמנת רק עד {EditCutoffMinutes} דקות לפני המועד. אפשר לבטל אותה וליצור חדשה.");

        var contentError = ValidateContent(req.Body, req.LinkUrl);
        if (contentError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, contentError);
        var (conditions, audienceError) = await ResolveAudienceAsync(scope.AccountId, req.Audience, cancelToken);
        if (audienceError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, audienceError);

        var settings = await _storage.GetSettingsAsync(scope.AccountId, cancelToken);
        var requestedAt = req.ScheduledAt.HasValue && req.ScheduledAt.Value > now ? DateTime.SpecifyKind(req.ScheduledAt.Value, DateTimeKind.Utc) : now;
        var allowedAt = MarketingSendWindow.NextAllowed(requestedAt, settings);
        var body = req.Body.Trim();
        var audienceType = (req.Audience?.Type ?? "all").Trim().ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(req.Name) ? DefaultSendName(body) : req.Name.Trim();

        var ok = await _storage.UpdateSendAsync(sendId, MarketingStorage.SendStatus.Scheduled, s =>
        {
            s.Name = name.Length > 200 ? name.Substring(0, 200) : name;
            s.AudienceType = audienceType;
            s.SegmentId = audienceType == "segment" ? req.Audience!.SegmentId : null;
            s.SystemSegmentKey = audienceType == "system" ? req.Audience!.SystemKey : null;
            s.AudienceLabel = Truncate(req.AudienceLabel?.Trim(), 300);
            s.AudienceDefinitionJson = MarketingSegmentQuery.Serialize(conditions);
            s.SiteIdsJson = JsonSerializer.Serialize(scope.SiteIds.OrderBy(id => id));
            s.Body = body;
            s.LinkUrl = string.IsNullOrWhiteSpace(req.LinkUrl) ? null : req.LinkUrl.Trim();
            s.ScheduledAt = allowedAt;
            s.OriginalScheduledAt = allowedAt == requestedAt ? null : requestedAt;
            s.UpdateTime = now;
        }, cancelToken);
        if (!ok)
            return CreateResponse(response, StatusCode.InvalidRequest, "השליחה כבר יצאה לדרך ואי אפשר לערוך אותה");

        var updated = await _storage.GetSendAsync(scope.AccountId, sendId, cancelToken);
        response.Data = MapSend(updated!, null);
        return response;
    }

    /// <summary>"שלח לעצמי לבדיקה" - the rendered message to one phone. Not charged to the marketing bank, not a delivery.</summary>
    public async Task<IApiResponse<MarketingTestSendRes>> SendTestAsync(MarketingTestSendReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingTestSendRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, req.SiteIds, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var contentError = ValidateContent(req.Body, req.LinkUrl);
        if (contentError != null)
            return CreateResponse(response, StatusCode.InvalidRequest, contentError);

        var phone = MarketingStorage.NormalizePhone(req.Phone);
        if (!MarketingStorage.IsSmsCapablePhone(phone))
            return CreateResponse(response, StatusCode.InvalidRequest, "מספר הטלפון אינו מספר נייד תקין");

        var config = await _accountSms.GetAccountConfigAsync(scope.AccountId, cancelToken);
        if (!SmsProvider.CanSendWith(config))
            return CreateResponse(response, StatusCode.InvalidRequest, "לא מוגדר חשבון SMS לשליחה");

        var text = await RenderSampleAsync(scope, req.Body, req.LinkUrl, cancelToken, useRealLink: true);
        bool sent;
        try
        {
            sent = await _accountSms.SendLoggedAsync(
                new SmsLogContext { Kind = MessageKind.System, Category = MessageCategory.MarketingTest, AccountId = scope.AccountId },
                phone, text, config, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marketing test SMS failed for account {AccountId}.", scope.AccountId);
            sent = false;
        }
        if (!sent)
            return CreateResponse(response, StatusCode.InvalidRequest, "שליחת הודעת הבדיקה נכשלה");

        response.Data = new MarketingTestSendRes { Sent = true, Preview = text };
        return response;
    }


    //*************************    Results    *************************//

    public async Task<IApiResponse<MarketingSendListRes>> ListSendsAsync(MarketingSendListReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSendListRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var take = Math.Clamp(req.Take, 1, 100);
        var statusFilter = req.Status?.Trim().ToLowerInvariant();
        var onlySites = scope.IsAccountAdmin ? null : scope.SiteIds;
        var (items, total) = await _storage.ListSendsAsync(scope.AccountId, req.FromUtc, req.ToUtc, req.Type, Math.Max(0, req.Skip), take, cancelToken, statusFilter, onlySites);
        var stats = await _storage.GetSendStatsAsync(items.Select(s => s.Id).ToList(), includeSkipReasons: false, cancelToken);

        // Period totals cover every send of the period, not just the visible page.
        var (allInPeriod, _) = await _storage.ListSendsAsync(scope.AccountId, req.FromUtc, req.ToUtc, req.Type, 0, 5000, cancelToken, statusFilter, onlySites);
        var periodStats = await _storage.GetSendStatsAsync(allInPeriod.Select(s => s.Id).ToList(), includeSkipReasons: false, cancelToken);

        response.Data = new MarketingSendListRes
        {
            Items = items.Select(s => MapSend(s, stats.GetValueOrDefault(s.Id))).ToList(),
            Total = total,
            Totals = new MarketingSendTotalsRes
            {
                SentCount = periodStats.Values.Sum(s => s.Sent),
                FailedCount = periodStats.Values.Sum(s => s.Failed),
                OrdersCount = periodStats.Values.Sum(s => s.Orders),
                Revenue = periodStats.Values.Sum(s => s.Revenue),
                Units = periodStats.Values.Sum(s => s.Units),
            },
        };
        return response;
    }

    /// <summary>Attributed revenue per Israeli week for the chart on the results home, plus the previous period for the trend.</summary>
    public async Task<IApiResponse<MarketingWeeklyRes>> GetWeeklyRevenueAsync(MarketingSendListReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingWeeklyRes>();
        var (scope, status, error) = await ResolveScopeAsync(req.AccountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var to = req.ToUtc ?? DateTime.UtcNow;
        var from = req.FromUtc ?? to.AddDays(-60);
        if (from >= to)
            return CreateResponse(response, StatusCode.InvalidRequest, "טווח תאריכים לא חוקי");
        var span = to - from;

        var current = await _storage.GetAttributionsInPeriodAsync(scope.AccountId, from, to, cancelToken);
        var previous = await _storage.GetAttributionsInPeriodAsync(scope.AccountId, from - span, from, cancelToken);

        // Buckets: every Sunday (Israel time) from the one on/before the period start up to its end, so quiet weeks show as zero bars.
        var firstLocal = MarketingSendWindow.ToIsrael(from).Date;
        firstLocal = firstLocal.AddDays(-(int)firstLocal.DayOfWeek);
        var lastLocal = MarketingSendWindow.ToIsrael(to).Date;
        var buckets = new SortedDictionary<DateTime, (decimal Revenue, int Orders)>();
        for (var d = firstLocal; d <= lastLocal; d = d.AddDays(7))
            buckets[d] = (0m, 0);
        foreach (var a in current)
        {
            var local = MarketingSendWindow.ToIsrael(a.OrderCreatedAt).Date;
            var week = local.AddDays(-(int)local.DayOfWeek);
            var cur = buckets.TryGetValue(week, out var v) ? v : (0m, 0);
            buckets[week] = (cur.Item1 + a.Revenue, cur.Item2 + 1);
        }

        response.Data = new MarketingWeeklyRes
        {
            Weeks = buckets.Select(b => new MarketingWeekPointRes { WeekStart = b.Key.ToString("yyyy-MM-dd"), Revenue = b.Value.Revenue, Orders = b.Value.Orders }).ToList(),
            Revenue = current.Sum(a => a.Revenue),
            Orders = current.Count,
            PreviousRevenue = previous.Sum(a => a.Revenue),
            PreviousOrders = previous.Count,
        };
        return response;
    }

    public async Task<IApiResponse<MarketingSendRes>> GetSendAsync(int sendId, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSendRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var send = await _storage.GetSendAsync(scope.AccountId, sendId, cancelToken);
        if (send == null || (!scope.IsAccountAdmin && !MarketingStorage.SendTouchesSites(send, scope.SiteIds)))
            return CreateResponse(response, StatusCode.ItemNotFound, "השליחה לא נמצאה");

        var stats = await _storage.GetSendStatsAsync(new[] { send.Id }, includeSkipReasons: true, cancelToken);
        response.Data = MapSend(send, stats.GetValueOrDefault(send.Id));
        return response;
    }

    public async Task<IApiResponse<MarketingDeliveryListRes>> GetDeliveriesAsync(int sendId, int? accountId, string? tab, int skip, int take, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingDeliveryListRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var send = await _storage.GetSendAsync(scope.AccountId, sendId, cancelToken);
        if (send == null || (!scope.IsAccountAdmin && !MarketingStorage.SendTouchesSites(send, scope.SiteIds)))
            return CreateResponse(response, StatusCode.ItemNotFound, "השליחה לא נמצאה");

        // A site admin sees only the recipients of their own branches (names + phones are personal data).
        var (rows, total) = await _storage.GetDeliveriesAsync(sendId, (tab ?? "all").Trim().ToLowerInvariant(), Math.Max(0, skip), Math.Clamp(take, 1, 200), cancelToken, scope.IsAccountAdmin ? null : scope.SiteIds);
        response.Data = new MarketingDeliveryListRes
        {
            Total = total,
            Items = rows.Select(r => new MarketingDeliveryRes
            {
                Id = r.Delivery.Id,
                CustomerId = r.Delivery.CustomerId,
                SiteId = r.Delivery.SiteId,
                CustomerName = r.Delivery.CustomerName,
                Phone = r.Delivery.NormalizedPhone,
                Status = r.Delivery.Status,
                SkipReason = r.Delivery.SkipReason,
                Error = r.Delivery.Error,
                SentAt = r.Delivery.Status is MarketingStorage.DeliveryStatus.Sent or MarketingStorage.DeliveryStatus.Delivered ? r.Delivery.SentAt : null,
                ClickCount = r.Delivery.ClickCount,
                LastClickedAt = r.Delivery.LastClickedAt,
                UnsubscribedAt = r.Delivery.UnsubscribedAt,
                OrderId = r.OrderId,
                OrderNumber = r.OrderNumber,
                Revenue = r.Revenue,
                OrderCreatedAt = r.OrderCreatedAt,
            }).ToList(),
        };
        return response;
    }

    public async Task<IApiResponse<bool>> CancelSendAsync(int sendId, int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<bool>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var target = await _storage.GetSendAsync(scope.AccountId, sendId, cancelToken);
        if (target == null || (!scope.IsAccountAdmin && !MarketingStorage.SendTouchesSites(target, scope.SiteIds)))
            return CreateResponse(response, StatusCode.ItemNotFound, "השליחה לא נמצאה");
        if (!await _storage.CancelSendAsync(scope.AccountId, sendId, cancelToken))
            return CreateResponse(response, StatusCode.InvalidRequest, "אי אפשר לבטל שליחה שכבר הסתיימה");
        response.Data = true;
        return response;
    }


    //*************************    Quota / settings / usage    *************************//

    public async Task<IApiResponse<MarketingQuotaRes>> GetQuotaAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingQuotaRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        response.Data = await BuildQuotaAsync(scope.AccountId, includeLedger: true, cancelToken);
        return response;
    }

    /// <summary>Super-admin only. Until self-service purchase exists (phase 7) this is how a shop's bank is loaded. A negative amount corrects a mistake.</summary>
    public async Task<IApiResponse<MarketingQuotaRes>> AllocateQuotaAsync(int accountId, MarketingQuotaAllocateReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingQuotaRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        if (!scope.Unrestricted)
            return CreateResponse(response, StatusCode.UnauthorizedData, "רק מנהל מערכת יכול לטעון הודעות");
        if (req.Amount == 0 || Math.Abs(req.Amount) > 1_000_000)
            return CreateResponse(response, StatusCode.InvalidRequest, "כמות לא חוקית");

        var allocConfig = await _accountSms.GetAccountConfigAsync(scope.AccountId, cancelToken);
        var platformSubAccount = allocConfig is { IsInforu: true, BilledByPlatform: true };

        // Platform sub-account: Inforu holds the balance, so the top-up goes THERE first; the ledger row is written only
        // once Inforu accepted it, so the audit trail never claims a load that did not happen.
        if (platformSubAccount)
        {
            if (req.Amount < 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "הורדת מכסה מתת-חשבון InforU נעשית בפורטל של InforU");
            var (inforuError, _) = await _inforuQuota.AddAsync(scope.AccountId, allocConfig!, req.Amount, cancelToken);
            if (inforuError != null)
                return CreateResponse(response, StatusCode.InvalidRequest, inforuError);
        }
        else
        {
            var balance = await _storage.GetBankBalanceAsync(scope.AccountId, cancelToken);
            if (balance + req.Amount < 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "אי אפשר להוריד מתחת לאפס");
        }

        await _storage.AddLedgerEntryAsync(new MarketingQuotaLedger
        {
            AccountId = scope.AccountId,
            EntryType = req.Amount > 0 ? MarketingStorage.LedgerEntryType.Purchase : MarketingStorage.LedgerEntryType.Refund,
            Bucket = MarketingStorage.BucketBank,
            Amount = req.Amount,
            Note = Truncate(req.Note?.Trim(), 300),
            CreationUserId = AuthUser.Id > 0 ? AuthUser.Id : null,
        }, cancelToken);

        response.Data = await BuildQuotaAsync(scope.AccountId, includeLedger: true, cancelToken);
        return response;
    }

    public async Task<IApiResponse<MarketingSettingsRes>> GetSettingsAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSettingsRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);
        response.Data = MapSettings(await _storage.GetSettingsAsync(scope.AccountId, cancelToken));
        return response;
    }

    public async Task<IApiResponse<MarketingSettingsRes>> UpdateSettingsAsync(int? accountId, MarketingSettingsReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingSettingsRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        if (!scope.IsAccountAdmin)
            return CreateResponse(response, StatusCode.UnauthorizedData, ErrAccountAdminOnly);
        if (!TimeSpan.TryParseExact(req.SendWindowStart, @"hh\:mm", null, out var start)
            || !TimeSpan.TryParseExact(req.SendWindowEnd, @"hh\:mm", null, out var end) || end <= start)
            return CreateResponse(response, StatusCode.InvalidRequest, "שעות השליחה לא חוקיות");
        // The law (חוק הספאם) has no hour limit, good manners do: never before 08:00 or after 21:00.
        if (start < new TimeSpan(8, 0, 0) || end > new TimeSpan(21, 0, 0))
            return CreateResponse(response, StatusCode.InvalidRequest, "שעות השליחה חייבות להיות בין 08:00 ל-21:00");

        var saved = await _storage.UpsertSettingsAsync(new MarketingSettings
        {
            AccountId = scope.AccountId,
            SendWindowStart = req.SendWindowStart,
            SendWindowEnd = req.SendWindowEnd,
            BlockShabbatAndHolidays = req.BlockShabbatAndHolidays,
            FrequencyCapCount = Math.Clamp(req.FrequencyCapCount, 1, 10),
            FrequencyCapDays = Math.Clamp(req.FrequencyCapDays, 1, 30),
            AttributionWindowHours = Math.Clamp(req.AttributionWindowHours, 1, MaxAttributionWindowHours),
            SkipOrderedToday = req.SkipOrderedToday,
        }, cancelToken);
        response.Data = MapSettings(saved);
        return response;
    }

    public async Task<IApiResponse<MarketingUsageRes>> GetUsageAsync(int? accountId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<MarketingUsageRes>();
        var (scope, status, error) = await ResolveScopeAsync(accountId, null, cancelToken);
        if (scope == null)
            return CreateResponse(response, status, error);

        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var rows = await _storage.GetUsageAsync(scope.AccountId, firstMonth, cancelToken);

        var res = new MarketingUsageRes();
        for (var m = firstMonth; m <= now; m = m.AddMonths(1))
        {
            var ofMonth = rows.Where(r => r.Year == m.Year && r.Month == m.Month).ToList();
            res.Months.Add(new MarketingUsageMonthRes
            {
                Period = $"{m.Year:D4}-{m.Month:D2}",
                OperationalUnits = ofMonth.Where(r => r.Kind == MessageKind.Operational).Sum(r => r.Units),
                MarketingUnits = ofMonth.Where(r => r.Kind == MessageKind.Marketing).Sum(r => r.Units),
            });
        }
        res.OperationalByCategory = rows
            .Where(r => r.Year == now.Year && r.Month == now.Month && r.Kind == MessageKind.Operational)
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Messages));

        response.Data = res;
        return response;
    }


    //*************************    Public (no login)    *************************//

    /// <summary>Tracked link: registers the click and returns where to redirect; null for an unknown token.</summary>
    public async Task<string?> ResolveClickAsync(string? token, CancellationToken cancelToken)
    {
        if (!MarketingMessageRenderer.IsValidToken(token))
            return null;
        var delivery = await _storage.GetDeliveryByTokenAsync(token!, cancelToken);
        if (delivery == null)
            return null;
        var url = await _storage.GetSendLinkUrlAsync(delivery.SendId, cancelToken);
        if (string.IsNullOrWhiteSpace(url))
            return null;
        await _storage.RegisterClickAsync(delivery.Id, cancelToken);
        return url;
    }

    public async Task<MarketingUnsubscribeInfo> GetUnsubscribeInfoAsync(string? token, CancellationToken cancelToken)
    {
        if (string.Equals(token, TestToken, StringComparison.OrdinalIgnoreCase))
            return new MarketingUnsubscribeInfo { Found = true, IsTestToken = true };
        if (!MarketingMessageRenderer.IsValidToken(token))
            return new MarketingUnsubscribeInfo();
        var delivery = await _storage.GetDeliveryByTokenAsync(token!, cancelToken);
        if (delivery == null)
            return new MarketingUnsubscribeInfo();
        var siteNames = await _storage.GetSiteNamesAsync(delivery.AccountId, cancelToken);
        // Opted out through ANY message of this account counts - the person is gone from the whole account (spec 07).
        var optedOutAt = await _storage.GetOptOutAtAsync(delivery.AccountId, delivery.NormalizedPhone, cancelToken);
        return new MarketingUnsubscribeInfo
        {
            Found = true,
            AlreadyOptedOut = optedOutAt != null,
            OptedOutAt = optedOutAt,
            StoreName = siteNames.GetValueOrDefault(delivery.SiteId),
            StoreUrl = await _storage.GetSiteUrlAsync(delivery.SiteId, cancelToken),
        };
    }

    /// <summary>One click, no login (spec §07). Removes the phone from marketing in the whole account.</summary>
    public async Task<MarketingUnsubscribeInfo> UnsubscribeAsync(string? token, CancellationToken cancelToken)
    {
        var info = await GetUnsubscribeInfoAsync(token, cancelToken);
        if (!info.Found || info.IsTestToken)
            return info;
        var delivery = await _storage.GetDeliveryByTokenAsync(token!, cancelToken);
        await _storage.OptOutByPhoneAsync(delivery!.AccountId, delivery.NormalizedPhone, "link", delivery.Id, cancelToken);
        info.AlreadyOptedOut = true;
        info.OptedOutAt ??= DateTime.UtcNow;
        return info;
    }


    //*************************    Private Methods    *************************//

    private async Task<MarketingQuotaRes> BuildQuotaAsync(int accountId, bool includeLedger, CancellationToken cancelToken)
    {
        var config = await _accountSms.GetAccountConfigAsync(accountId, cancelToken);
        var totals = await _storage.GetQuotaTotalsAsync(accountId, DateTime.UtcNow, cancelToken);
        var res = new MarketingQuotaRes
        {
            // Own provider account = the shop pays the provider directly. A platform-opened sub-account is still ours to meter.
            IsExempt = config != null && !config.BilledByPlatform,
            Balance = totals.Purchased - totals.Consumed,
            Purchased = totals.Purchased,
            Consumed = totals.Consumed,
            LastPurchaseAt = totals.LastPurchaseAt,
            MonthlyBurn = totals.ConsumedLast60Days / 2,
        };
        if (config is { IsInforu: true })
        {
            res.ProviderQuota = await _inforuQuota.GetAsync(accountId, config, cancelToken);
            // A platform-opened sub-account has ONE balance - the one Inforu enforces. Our ledger stays as the audit trail.
            if (config.BilledByPlatform)
            {
                if (res.ProviderQuota.Available && res.ProviderQuota.Remaining.HasValue)
                {
                    res.Balance = res.ProviderQuota.Remaining.Value;
                    res.BalanceSource = "inforu";
                }
                else
                {
                    res.BalanceSource = "inforu_stale";
                }
            }
        }
        if (includeLedger)
        {
            res.Ledger = (await _storage.GetLedgerAsync(accountId, 50, cancelToken))
                .Select(l => new MarketingLedgerEntryRes
                {
                    Id = l.Id, CreationTime = l.CreationTime, EntryType = l.EntryType, Amount = l.Amount, RefSendId = l.RefSendId, Note = l.Note,
                }).ToList();
        }
        return res;
    }

    private async Task<string> RenderSampleAsync(Scope scope, string? body, string? linkUrl, CancellationToken cancelToken, bool useRealLink = false)
    {
        var siteNames = await _storage.GetSiteNamesAsync(scope.AccountId, cancelToken);
        var storeName = scope.SiteIds.OrderBy(id => id).Select(id => siteNames.GetValueOrDefault(id)).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var baseUrl = ShortLinkBaseUrl(_configuration);
        string? link = null;
        if (!string.IsNullOrWhiteSpace(linkUrl))
            link = useRealLink ? linkUrl.Trim() : MarketingMessageRenderer.TrackedLinkUrl(baseUrl, SampleToken);
        // A real test message carries the test token, so tapping its unsubscribe link explains instead of "not found".
        return MarketingMessageRenderer.Render(body ?? string.Empty, SampleCustomerName, storeName, link, MarketingMessageRenderer.UnsubscribeUrl(baseUrl, useRealLink ? TestToken : SampleToken));
    }

    private static string? ValidateContent(string? body, string? linkUrl)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "יש לכתוב את תוכן ההודעה";
        if (body.Length > MarketingMessageRenderer.MaxBodyLength)
            return $"ההודעה ארוכה מדי (עד {MarketingMessageRenderer.MaxBodyLength} תווים)";
        if (!string.IsNullOrWhiteSpace(linkUrl)
            && !(Uri.TryCreate(linkUrl.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            return "הקישור חייב להתחיל ב-http:// או https://";
        if (string.IsNullOrWhiteSpace(linkUrl) && body.Contains(MarketingMessageRenderer.TokenLink, StringComparison.OrdinalIgnoreCase))
            return "ההודעה כוללת [link] אבל לא הוגדר קישור";
        return null;
    }

    private static string DefaultSendName(string body)
    {
        var line = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 40 ? line : line.Substring(0, 40) + "…";
    }

    private static string? Truncate(string? s, int max) => s != null && s.Length > max ? s.Substring(0, max) : s;

    private static MarketingSettingsRes MapSettings(MarketingSettings s) => new()
    {
        SendWindowStart = s.SendWindowStart,
        SendWindowEnd = s.SendWindowEnd,
        BlockShabbatAndHolidays = s.BlockShabbatAndHolidays,
        FrequencyCapCount = s.FrequencyCapCount,
        FrequencyCapDays = s.FrequencyCapDays,
        AttributionWindowHours = s.AttributionWindowHours,
        SkipOrderedToday = s.SkipOrderedToday,
    };

    private static MarketingSendRes MapSend(MarketingSend s, MarketingStorage.SendStats? stats)
    {
        List<int> siteIds;
        try { siteIds = JsonSerializer.Deserialize<List<int>>(s.SiteIdsJson) ?? new List<int>(); }
        catch (JsonException) { siteIds = new List<int>(); }
        var conditions = MarketingSegmentQuery.Parse(s.AudienceDefinitionJson);
        var resendCondition = conditions.FirstOrDefault(c => string.Equals(c.Axis, SegmentAxis.Resend, StringComparison.OrdinalIgnoreCase));
        int? sourceSendId = resendCondition != null && int.TryParse(resendCondition.Value, out var src) ? src : null;

        return new MarketingSendRes
        {
            Id = s.Id,
            Type = s.Type,
            Name = s.Name,
            Channel = s.Channel,
            Status = s.Status,
            PausedReason = s.PausedReason,
            AudienceType = s.AudienceType,
            AudienceLabel = s.AudienceLabel,
            SystemSegmentKey = s.SystemSegmentKey,
            SegmentId = s.SegmentId,
            AudienceConditions = conditions,
            SourceSendId = sourceSendId,
            CanEdit = IsEditable(s, DateTime.UtcNow),
            SiteIds = siteIds,
            Body = s.Body,
            LinkUrl = s.LinkUrl,
            ScheduledAt = s.ScheduledAt,
            OriginalScheduledAt = s.OriginalScheduledAt,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
            AttributionWindowHours = s.AttributionWindowHours,
            AudienceCount = s.AudienceCount,
            QueuedCount = stats?.Queued ?? 0,
            SentCount = stats?.Sent ?? 0,
            // No delivery reports from the SMS providers yet - "delivered" stays unknown rather than wrong.
            DeliveredCount = stats is { Delivered: > 0 } ? stats.Delivered : null,
            FailedCount = stats?.Failed ?? 0,
            SkippedCount = stats?.Skipped ?? 0,
            ClickedCount = stats?.Clicked ?? 0,
            UnsubscribedCount = stats?.Unsubscribed ?? 0,
            OrdersCount = stats?.Orders ?? 0,
            OrderedRecipientsCount = stats?.OrderedRecipients ?? 0,
            Revenue = stats?.Revenue ?? 0m,
            Units = stats?.Units ?? 0,
            SkippedByReason = stats?.SkippedByReason,
        };
    }
}
