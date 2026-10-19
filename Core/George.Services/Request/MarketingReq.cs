using George.Data;

namespace George.Services.Request
{
    /// <summary>Who a send (or a preview) addresses. <c>all</c> | <c>system</c> (ready-made segment) | <c>segment</c> (saved) | <c>filter</c> (ad-hoc conditions, not saved).</summary>
    public class MarketingAudienceReq
    {
        /// <summary>all | system | segment | filter | resend.</summary>
        public string Type { get; set; } = "all";
        public string? SystemKey { get; set; }
        public int? SegmentId { get; set; }
        public List<SegmentCondition>? Conditions { get; set; }
        /// <summary>For <c>resend</c>: the earlier send whose non-buying recipients make the audience.</summary>
        public int? SourceSendId { get; set; }
        /// <summary>For <c>customers</c>: the picked customer ids (up to 500).</summary>
        public List<int>? CustomerIds { get; set; }
    }

    /// <summary>Common scope of every marketing call. <c>AccountId</c> is honoured only for unrestricted callers (impersonation).</summary>
    public class MarketingScopeReq
    {
        public int? AccountId { get; set; }
        /// <summary>Branch scope; empty = every branch the caller may access. A site manager is always narrowed to their own branches.</summary>
        public List<int>? SiteIds { get; set; }
    }

    public class MarketingSegmentPreviewReq : MarketingScopeReq
    {
        public MarketingAudienceReq Audience { get; set; } = new();
        /// <summary>Optional name / phone search inside the audience (the "specific customers" picker).</summary>
        public string? Search { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 10;
    }

    public class MarketingSegmentSaveReq : MarketingScopeReq
    {
        public string Name { get; set; } = string.Empty;
        public List<SegmentCondition> Conditions { get; set; } = new();
        public bool IsPinned { get; set; }
    }

    public class MarketingEstimateReq : MarketingScopeReq
    {
        public MarketingAudienceReq Audience { get; set; } = new();
        public string? Body { get; set; }
        public string? LinkUrl { get; set; }
        /// <summary>UTC. Null = now.</summary>
        public DateTime? ScheduledAt { get; set; }
    }

    public class MarketingSendCreateReq : MarketingScopeReq
    {
        public string? Name { get; set; }
        public MarketingAudienceReq Audience { get; set; } = new();
        /// <summary>Display text of the audience ("חברי מועדון שלא הזמינו 60 יום") - kept with the send for the results screens.</summary>
        public string? AudienceLabel { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        /// <summary>UTC. Null = send now.</summary>
        public DateTime? ScheduledAt { get; set; }
        /// <summary>Keep as a draft (status draft): nothing is validated beyond the scope, nothing is dispatched.</summary>
        public bool SaveAsDraft { get; set; }
        /// <summary>Ignore the hours / Shabbat rule. The wizard sets it for "שלח עכשיו" and for a scheduled send the shop marked "שלח בכל זאת".</summary>
        public bool BypassSendWindow { get; set; }
    }

    public class MarketingTestSendReq : MarketingScopeReq
    {
        public string Body { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        /// <summary>One number, or several separated by commas (max 5).</summary>
        public string Phone { get; set; } = string.Empty;
    }

    public class MarketingSendListReq : MarketingScopeReq
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? Type { get; set; }
        /// <summary>null = everything; "scheduled" = not yet started; "finished" = everything that already started (spec 10.1 keeps the two apart).</summary>
        public string? Status { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }

    public class MarketingSettingsReq
    {
        public string SendWindowStart { get; set; } = "09:00";
        public string SendWindowEnd { get; set; } = "20:00";
        public bool BlockShabbatAndHolidays { get; set; } = true;
        public int FrequencyCapCount { get; set; } = 2;
        public int FrequencyCapDays { get; set; } = 7;
        public int AttributionWindowHours { get; set; } = 72;
        public bool SkipOrderedToday { get; set; } = true;
    }

    /// <summary>Super-admin: load (or correct) an account's marketing message bank.</summary>
    public class MarketingQuotaAllocateReq
    {
        public int Amount { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Super-admin: open an Inforu sub-account for the shop and save it as its platform-billed SMS account.</summary>
    public class MarketingInforuProvisionReq
    {
        public string CustomerName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>Empty = Giorgio generates one.</summary>
        public string? ApiToken { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        /// <summary>Sender id shown to recipients (≤11 Latin chars).</summary>
        public string Sender { get; set; } = "giorgio";
        /// <summary>2 = monthly renewal, 3 = packages.</summary>
        public int QuotaTypeId { get; set; } = 3;
        public int QuotaAmount { get; set; }
        public int WarningLevel { get; set; }
    }

    /// <summary>Super-admin: set the sub-account's quota type/amount in Inforu (Customer/Update).</summary>
    public class MarketingInforuQuotaReq
    {
        public int QuotaTypeId { get; set; } = 2;
        public int Amount { get; set; }
        public int? WarningLevel { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Shop (account admin): start buying a package - answers with the hosted payment page.</summary>
    public class MarketingPurchaseStartReq
    {
        public int? AccountId { get; set; }
        public int PackageId { get; set; }
        /// <summary>The browser origin the shop is on; the hosted page returns there (falls back to App:PublicBaseUrl).</summary>
        public string? AppOrigin { get; set; }
    }

    /// <summary>Super-admin: one catalog row (id 0 = new; rows missing from the saved list are deactivated).</summary>
    public class MarketingPackageSaveReq
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Messages { get; set; }
        /// <summary>ILS before VAT.</summary>
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Badge { get; set; }
    }
}
