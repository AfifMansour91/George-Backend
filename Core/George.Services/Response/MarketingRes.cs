using George.Data;

namespace George.Services.Response
{
    public class MarketingSegmentRes
    {
        /// <summary>Saved segment id; null for a ready-made (system) segment.</summary>
        public int? Id { get; set; }
        /// <summary>Ready-made segment key (<c>vip</c>, <c>dormant</c>...); null for a saved segment. The UI owns the display name.</summary>
        public string? SystemKey { get; set; }
        public string? Name { get; set; }
        public List<SegmentCondition> Conditions { get; set; } = new();
        public bool IsPinned { get; set; }
        /// <summary>How many customers are in the segment.</summary>
        public int Count { get; set; }
        /// <summary>How many of them may actually be messaged - the real number.</summary>
        public int ConsentCount { get; set; }
    }

    public class MarketingSegmentsRes
    {
        public List<MarketingSegmentRes> System { get; set; } = new();
        public List<MarketingSegmentRes> Saved { get; set; } = new();
        public int AllCustomersCount { get; set; }
        public int AllCustomersConsentCount { get; set; }
        public int MaxConditions { get; set; }
        public int MaxPinned { get; set; }
    }

    public class MarketingSegmentCustomerRes
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public DateTime? LastOrderAt { get; set; }
        public bool HasConsent { get; set; }
    }

    public class MarketingSegmentPreviewRes
    {
        public int Count { get; set; }
        public int ConsentCount { get; set; }
        public List<MarketingSegmentCustomerRes> Items { get; set; } = new();
    }

    public class MarketingQuotaRes
    {
        /// <summary>The account sends through its own SMS provider account, so George does not meter it.</summary>
        public bool IsExempt { get; set; }
        /// <summary>
        /// Where <see cref="Balance"/> comes from: <c>inforu</c> = the live remaining quota of the platform-opened Inforu
        /// sub-account (one number, whatever was loaded there - from George or straight in the Inforu portal);
        /// <c>george</c> = George's own ledger (system SMS account). <c>inforu_stale</c> = Inforu did not answer, showing the ledger instead.
        /// </summary>
        public string BalanceSource { get; set; } = "george";
        public int Balance { get; set; }
        public int Purchased { get; set; }
        public int Consumed { get; set; }
        public DateTime? LastPurchaseAt { get; set; }
        /// <summary>Average monthly burn over the last 60 days; 0 when there is no history.</summary>
        public int MonthlyBurn { get; set; }
        public List<MarketingLedgerEntryRes> Ledger { get; set; } = new();
        /// <summary>Live balance at the SMS provider (Inforu sub-account); null when the account is not on Inforu.</summary>
        public MarketingProviderQuotaRes? ProviderQuota { get; set; }
        /// <summary>After an allocation: why the provider side was NOT topped up (George's bank was). Null = synced or not applicable.</summary>
    }

    public class MarketingProviderQuotaRes
    {
        public string Provider { get; set; } = string.Empty;
        /// <summary>The provider answered; <see cref="Remaining"/> is current.</summary>
        public bool Available { get; set; }
        public int? Remaining { get; set; }
        /// <summary>Packages | Monthly | Unlimited.</summary>
        public string? QuotaType { get; set; }
        public int? WarningLevel { get; set; }
        public string? CustomerId { get; set; }
        /// <summary>Parent credentials are configured and the sub-account's customer id is known - "טען הודעות" will also top up the provider.</summary>
        public bool CanAllocate { get; set; }
        public string? Error { get; set; }
    }

    public class MarketingLedgerEntryRes
    {
        public long Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public int Amount { get; set; }
        public int? RefSendId { get; set; }
        public string? Note { get; set; }
    }

    public class MarketingEstimateRes
    {
        public int AudienceCount { get; set; }
        public int NoConsentCount { get; set; }
        /// <summary>Consented customers. The final number can only be lower (phone-less, duplicates, frequency cap) - resolved at dispatch.</summary>
        public int WillSendCount { get; set; }
        public int UnitsPerMessage { get; set; }
        public int TotalUnits { get; set; }
        public int MessageLength { get; set; }
        public MarketingQuotaRes Quota { get; set; } = new();
        public bool QuotaSufficient { get; set; }
        public bool AllowedNow { get; set; }
        /// <summary>When the requested time is not allowed (Shabbat / holiday / quiet hours): the time the send will actually go out.</summary>
        public DateTime? DeferredTo { get; set; }
        public string SendWindowStart { get; set; } = "09:00";
        public string SendWindowEnd { get; set; } = "20:00";
        public bool BlockShabbatAndHolidays { get; set; }
        public int FrequencyCapCount { get; set; }
        public int FrequencyCapDays { get; set; }
        /// <summary>Sender name the recipient will see (the account's SMS sender, or the system account's).</summary>
        public string? SenderName { get; set; }
        /// <summary>Consenting recipients without an Israeli mobile number - dropped at send time (screen 13 "ללא מספר טלפון").</summary>
        public int NoPhoneCount { get; set; }
        /// <summary>Consenting recipients sharing a phone with another row - only one copy goes out.</summary>
        public int DuplicateCount { get; set; }
        /// <summary>The exact text a recipient would get, rendered for a sample customer.</summary>
        public string Preview { get; set; } = string.Empty;
    }

    public class MarketingSendRes
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PausedReason { get; set; }
        public string AudienceType { get; set; } = string.Empty;
        public string? AudienceLabel { get; set; }
        public string? SystemSegmentKey { get; set; }
        public int? SegmentId { get; set; }
        /// <summary>The conditions this send resolves (lets "duplicate" and "edit" rebuild the wizard).</summary>
        public List<SegmentCondition> AudienceConditions { get; set; } = new();
        /// <summary>For a "resend" audience: the earlier send.</summary>
        public int? SourceSendId { get; set; }
        /// <summary>Scheduled, not started, and more than 10 minutes away (spec 6.2).</summary>
        public bool CanEdit { get; set; }
        public List<int> SiteIds { get; set; } = new();
        public string Body { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime? OriginalScheduledAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int AttributionWindowHours { get; set; }

        public int AudienceCount { get; set; }
        public int QueuedCount { get; set; }
        public int SentCount { get; set; }
        /// <summary>Null while the SMS provider gives no delivery reports - the UI then shows "sent" only.</summary>
        public int? DeliveredCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public int ClickedCount { get; set; }
        public int UnsubscribedCount { get; set; }
        public int OrdersCount { get; set; }
        /// <summary>Unique recipients with at least one attributed order (spec 10.2 "הזמינו"); OrdersCount may be higher.</summary>
        public int OrderedRecipientsCount { get; set; }
        public decimal Revenue { get; set; }
        public int Units { get; set; }
        public Dictionary<string, int>? SkippedByReason { get; set; }
    }

    public class MarketingSendListRes
    {
        public List<MarketingSendRes> Items { get; set; } = new();
        public int Total { get; set; }
        public MarketingSendTotalsRes Totals { get; set; } = new();
    }

    /// <summary>KPI row of the results screen, over the listed period.</summary>
    public class MarketingSendTotalsRes
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
        public int Units { get; set; }
    }

    /// <summary>Screen 12 "הכנסה משיווק לפי שבוע": attributed revenue bucketed by Israeli week (Sunday start).</summary>
    public class MarketingWeeklyRes
    {
        public List<MarketingWeekPointRes> Weeks { get; set; } = new();
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
        /// <summary>Same-length period right before FromUtc - for the "↑18%" trend.</summary>
        public decimal PreviousRevenue { get; set; }
        public int PreviousOrders { get; set; }
    }

    public class MarketingWeekPointRes
    {
        /// <summary>Israel-local date of the week's Sunday (yyyy-MM-dd).</summary>
        public string WeekStart { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
    }

    public class MarketingDeliveryRes
    {
        public long Id { get; set; }
        public int CustomerId { get; set; }
        public int SiteId { get; set; }
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SkipReason { get; set; }
        public string? Error { get; set; }
        public DateTime? SentAt { get; set; }
        public int ClickCount { get; set; }
        public DateTime? LastClickedAt { get; set; }
        public DateTime? UnsubscribedAt { get; set; }
        public int? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public decimal? Revenue { get; set; }
        public DateTime? OrderCreatedAt { get; set; }
    }

    public class MarketingDeliveryListRes
    {
        public List<MarketingDeliveryRes> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class MarketingSettingsRes
    {
        public string SendWindowStart { get; set; } = "09:00";
        public string SendWindowEnd { get; set; } = "20:00";
        public bool BlockShabbatAndHolidays { get; set; } = true;
        public int FrequencyCapCount { get; set; } = 2;
        public int FrequencyCapDays { get; set; } = 7;
        public int AttributionWindowHours { get; set; } = 72;
        public bool SkipOrderedToday { get; set; } = true;
    }

    public class MarketingUsageMonthRes
    {
        /// <summary>YYYY-MM.</summary>
        public string Period { get; set; } = string.Empty;
        public int OperationalUnits { get; set; }
        public int MarketingUnits { get; set; }
    }

    public class MarketingUsageRes
    {
        public List<MarketingUsageMonthRes> Months { get; set; } = new();
        /// <summary>This month's operational messages by category (order_confirmation, invoice...).</summary>
        public Dictionary<string, int> OperationalByCategory { get; set; } = new();
    }

    public class MarketingTestSendRes
    {
        public bool Sent { get; set; }
        public string Preview { get; set; } = string.Empty;
    }

    /// <summary>Public unsubscribe page state.</summary>
    public class MarketingUnsubscribeInfo
    {
        public bool Found { get; set; }
        public bool AlreadyOptedOut { get; set; }
        /// <summary>When this phone opted out (any delivery of the account), for the "כבר הוסרת" page.</summary>
        public DateTime? OptedOutAt { get; set; }
        public string? StoreName { get; set; }
        /// <summary>The shop's website, for "חזרה לאתר החנות" on the public pages.</summary>
        public string? StoreUrl { get; set; }
        /// <summary>True for the token used in "שלח לעצמי לבדיקה" - the page explains instead of unsubscribing.</summary>
        public bool IsTestToken { get; set; }
    }
}
