using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>Marketing module storage: settings, saved segments, sends + deliveries, quota ledger, attribution, public token lookups.</summary>
public class MarketingStorage : StorageBase
{
    public static class SendStatus
    {
        public const string Scheduled = "scheduled";
        public const string Sending = "sending";
        public const string Sent = "sent";
        public const string Canceled = "canceled";
        public const string Failed = "failed";
    }

    public static class DeliveryStatus
    {
        public const string Queued = "queued";
        public const string Sending = "sending";
        public const string Sent = "sent";
        public const string Delivered = "delivered";
        public const string Failed = "failed";
        public const string Skipped = "skipped";
    }

    public static class SkipReason
    {
        public const string NoConsent = "no_consent";
        public const string NoPhone = "no_phone";
        public const string Duplicate = "duplicate";
        public const string FrequencyCap = "frequency_cap";
        public const string OrderedToday = "ordered_today";
        public const string Canceled = "canceled";
    }

    public static class LedgerEntryType
    {
        public const string Allocation = "allocation";
        public const string Purchase = "purchase";
        public const string Consumption = "consumption";
        public const string Refund = "refund";
    }

    public const string BucketBank = "bank";

    public MarketingStorage(GeorgeDBContext dbContext, ILogger<MarketingStorage> logger)
        : base(dbContext, logger)
    {
    }

    /// <summary>Canonical phone key - the same one <c>Customer.NormalizedPhone</c> is built with.</summary>
    public static string NormalizePhone(string? phone) => CustomerStorage.NormalizePhone(phone);

    /// <summary>An Israeli mobile number (05x, 10 digits) - the only kind an SMS can reach.</summary>
    public static bool IsSmsCapablePhone(string? normalizedPhone) =>
        normalizedPhone is { Length: 10 } && normalizedPhone.StartsWith("05", StringComparison.Ordinal);


    //*************************    Settings    *************************//

    /// <summary>The account's guard rules; a detached default instance when the account never saved any.</summary>
    public async Task<MarketingSettings> GetSettingsAsync(int accountId, CancellationToken cancelToken)
    {
        var row = await _dbContext.MarketingSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountId == accountId, cancelToken).ConfigureAwait(false);
        return row ?? new MarketingSettings { AccountId = accountId };
    }

    public async Task<MarketingSettings> UpsertSettingsAsync(MarketingSettings settings, CancellationToken cancelToken)
    {
        var row = await _dbContext.MarketingSettings
            .FirstOrDefaultAsync(s => s.AccountId == settings.AccountId, cancelToken).ConfigureAwait(false);
        if (row == null)
        {
            row = new MarketingSettings { AccountId = settings.AccountId };
            _dbContext.MarketingSettings.Add(row);
        }
        row.SendWindowStart = settings.SendWindowStart;
        row.SendWindowEnd = settings.SendWindowEnd;
        row.BlockShabbatAndHolidays = settings.BlockShabbatAndHolidays;
        row.FrequencyCapCount = settings.FrequencyCapCount;
        row.FrequencyCapDays = settings.FrequencyCapDays;
        row.AttributionWindowHours = settings.AttributionWindowHours;
        row.SkipOrderedToday = settings.SkipOrderedToday;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return row;
    }


    //*************************    Segments    *************************//

    public async Task<List<MarketingSegment>> GetSegmentsAsync(int accountId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSegment.AsNoTracking()
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.IsPinned).ThenBy(s => s.Name)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<MarketingSegment?> GetSegmentAsync(int accountId, int segmentId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSegment.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.Id == segmentId, cancelToken).ConfigureAwait(false);
    }

    public async Task<int> CountPinnedSegmentsAsync(int accountId, int? exceptSegmentId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSegment
            .CountAsync(s => s.AccountId == accountId && s.IsPinned && s.Id != (exceptSegmentId ?? 0), cancelToken).ConfigureAwait(false);
    }

    public async Task<MarketingSegment> AddSegmentAsync(MarketingSegment segment, CancellationToken cancelToken)
    {
        _dbContext.MarketingSegment.Add(segment);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return segment;
    }

    public async Task<MarketingSegment?> UpdateSegmentAsync(int accountId, int segmentId, Action<MarketingSegment> apply, CancellationToken cancelToken)
    {
        var row = await _dbContext.MarketingSegment
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.Id == segmentId, cancelToken).ConfigureAwait(false);
        if (row == null)
            return null;
        apply(row);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return row;
    }

    public async Task<bool> DeleteSegmentAsync(int accountId, int segmentId, CancellationToken cancelToken)
    {
        var row = await _dbContext.MarketingSegment
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.Id == segmentId, cancelToken).ConfigureAwait(false);
        if (row == null)
            return false;
        _dbContext.MarketingSegment.Remove(row); // soft delete (IsDeleted) via the context
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>The two numbers every segment shows: how many customers, and how many of them may actually be messaged.</summary>
    public async Task<(int Count, int ConsentCount)> CountSegmentAsync(
        int accountId, IReadOnlyCollection<int> siteIds, IEnumerable<SegmentCondition>? conditions,
        DateTime utcNow, DateTime israelToday, CancellationToken cancelToken)
    {
        var query = await BuildAudienceQueryAsync(accountId, siteIds, conditions, utcNow, israelToday, cancelToken).ConfigureAwait(false);
        var count = await query.CountAsync(cancelToken).ConfigureAwait(false);
        var consent = count == 0
            ? 0
            : await MarketingSegmentQuery.WhereSmsConsent(query).CountAsync(cancelToken).ConfigureAwait(false);
        return (count, consent);
    }

    /// <summary>
    /// What the dispatcher will drop from the CONSENTING audience for phone reasons - shown in the wizard so "יישלח בפועל"
    /// is honest before the send, not only in the results screen (screen 13 "ללא מספר טלפון / כפילויות").
    /// </summary>
    public async Task<(int NoPhone, int Duplicates)> CountAudiencePhoneIssuesAsync(
        int accountId, IReadOnlyCollection<int> siteIds, IEnumerable<SegmentCondition>? conditions,
        DateTime utcNow, DateTime israelToday, CancellationToken cancelToken)
    {
        var query = await BuildAudienceQueryAsync(accountId, siteIds, conditions, utcNow, israelToday, cancelToken).ConfigureAwait(false);
        var phones = await MarketingSegmentQuery.WhereSmsConsent(query)
            .Select(r => r.Customer.NormalizedPhone)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        int noPhone = 0, duplicates = 0;
        var seen = new HashSet<string>();
        foreach (var phone in phones)
        {
            if (!IsSmsCapablePhone(phone)) { noPhone++; continue; }
            if (!seen.Add(phone!)) duplicates++;
        }
        return (noPhone, duplicates);
    }

    public async Task<IQueryable<CustomerAggRow>> BuildAudienceQueryAsync(
        int accountId, IReadOnlyCollection<int> siteIds, IEnumerable<SegmentCondition>? conditions,
        DateTime utcNow, DateTime israelToday, CancellationToken cancelToken)
    {
        var query = MarketingSegmentQuery.BaseQuery(_dbContext, accountId, siteIds);
        return await MarketingSegmentQuery.ApplyAsync(_dbContext, query, conditions, utcNow, israelToday, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Distinct cities of the account's customers, most common first - feeds the geo axis picker.</summary>
    public async Task<List<string>> GetCustomerCitiesAsync(int accountId, IReadOnlyCollection<int> siteIds, CancellationToken cancelToken)
    {
        var rows = await _dbContext.Set<Customer>().AsNoTracking()
            .Where(c => c.AccountId == accountId && !c.IsDeleted && siteIds.Contains(c.SiteId) && c.City != null && c.City != "")
            .GroupBy(c => c.City!.Trim())
            .Select(g => new { City = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(300)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return rows.Select(r => r.City).Where(c => c.Length > 0).Distinct().ToList();
    }


    //*************************    Sends    *************************//

    public async Task<MarketingSend> AddSendAsync(MarketingSend send, CancellationToken cancelToken)
    {
        _dbContext.MarketingSend.Add(send);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return send;
    }

    public async Task<MarketingSend?> GetSendAsync(int accountId, int sendId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSend.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.Id == sendId, cancelToken).ConfigureAwait(false);
    }

    public static bool SendTouchesSites(MarketingSend send, IReadOnlyCollection<int> siteIds) => SendTouchesSites(send.SiteIdsJson, siteIds);

    /// <summary>Does a send whose branch list is <paramref name="siteIdsJson"/> (e.g. "[20,21]") touch any of <paramref name="siteIds"/>? The site admin's view.</summary>
    public static bool SendTouchesSites(string? siteIdsJson, IReadOnlyCollection<int> siteIds)
    {
        try
        {
            var sendSites = System.Text.Json.JsonSerializer.Deserialize<List<int>>(siteIdsJson ?? "[]") ?? new List<int>();
            return sendSites.Count == 0 || sendSites.Any(siteIds.Contains);
        }
        catch (System.Text.Json.JsonException) { return true; }
    }

    public async Task<(List<MarketingSend> Items, int Total)> ListSendsAsync(
        int accountId, DateTime? fromUtc, DateTime? toUtc, string? type, int skip, int take, CancellationToken cancelToken, string? statusFilter = null, IReadOnlyCollection<int>? onlySiteIds = null)
    {
        var query = _dbContext.MarketingSend.AsNoTracking().Where(s => s.AccountId == accountId);
        if (onlySiteIds != null)
        {
            // SiteIdsJson is a tiny JSON array and an account has few sends: match in memory, then filter by id (translates cleanly).
            var candidates = await _dbContext.MarketingSend.AsNoTracking().Where(s => s.AccountId == accountId).Select(s => new { s.Id, s.SiteIdsJson }).ToListAsync(cancelToken).ConfigureAwait(false);
            var matching = candidates.Where(c => SendTouchesSites(c.SiteIdsJson, onlySiteIds)).Select(c => c.Id).ToList();
            query = query.Where(s => matching.Contains(s.Id));
        }
        // The period is about when the send actually went out (a deferred send belongs to the day it left, not the day it was booked).
        if (fromUtc.HasValue) query = query.Where(s => (s.StartedAt ?? s.ScheduledAt) >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(s => (s.StartedAt ?? s.ScheduledAt) < toUtc.Value);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(s => s.Type == type);
        if (statusFilter == "scheduled") query = query.Where(s => s.Status == SendStatus.Scheduled);
        else if (statusFilter == "finished") query = query.Where(s => s.Status != SendStatus.Scheduled);

        var total = await query.CountAsync(cancelToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(s => s.ScheduledAt).ThenByDescending(s => s.Id)
            .Skip(skip).Take(take).ToListAsync(cancelToken).ConfigureAwait(false);
        return (items, total);
    }

    public class AttributionPoint
    {
        public DateTime OrderCreatedAt { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>Attributed orders of the account whose order time falls in [from, to) - the weekly revenue chart's raw rows.</summary>
    public async Task<List<AttributionPoint>> GetAttributionsInPeriodAsync(int accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingAttribution.AsNoTracking()
            .Where(a => a.AccountId == accountId && a.OrderCreatedAt >= fromUtc && a.OrderCreatedAt < toUtc)
            .Select(a => new AttributionPoint { OrderCreatedAt = a.OrderCreatedAt, Revenue = a.Revenue })
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public class SendStats
    {
        public int SendId { get; set; }
        public int Total { get; set; }
        public int Queued { get; set; }
        public int Sent { get; set; }
        public int Delivered { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Clicked { get; set; }
        public int Unsubscribed { get; set; }
        public int Units { get; set; }
        public int Orders { get; set; }
        /// <summary>Distinct deliveries (= recipients) with an attributed order.</summary>
        public int OrderedRecipients { get; set; }
        public decimal Revenue { get; set; }
        public Dictionary<string, int> SkippedByReason { get; set; } = new();
    }

    public async Task<Dictionary<int, SendStats>> GetSendStatsAsync(IReadOnlyCollection<int> sendIds, bool includeSkipReasons, CancellationToken cancelToken)
    {
        var result = sendIds.Distinct().ToDictionary(id => id, id => new SendStats { SendId = id });
        if (result.Count == 0)
            return result;

        var deliveryStats = await _dbContext.MarketingDelivery.AsNoTracking()
            .Where(d => sendIds.Contains(d.SendId))
            .GroupBy(d => d.SendId)
            .Select(g => new
            {
                SendId = g.Key,
                Total = g.Count(),
                Queued = g.Count(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Sending),
                Sent = g.Count(d => d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Delivered),
                Delivered = g.Count(d => d.Status == DeliveryStatus.Delivered),
                Failed = g.Count(d => d.Status == DeliveryStatus.Failed),
                Skipped = g.Count(d => d.Status == DeliveryStatus.Skipped),
                Clicked = g.Count(d => d.ClickCount > 0),
                Unsubscribed = g.Count(d => d.UnsubscribedAt != null),
                Units = g.Sum(d => d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Delivered ? d.CostUnits : 0),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        foreach (var s in deliveryStats)
        {
            var r = result[s.SendId];
            r.Total = s.Total; r.Queued = s.Queued; r.Sent = s.Sent; r.Delivered = s.Delivered; r.Failed = s.Failed;
            r.Skipped = s.Skipped; r.Clicked = s.Clicked; r.Unsubscribed = s.Unsubscribed; r.Units = s.Units;
        }

        var attribution = await _dbContext.MarketingAttribution.AsNoTracking()
            .Where(a => sendIds.Contains(a.SendId))
            .GroupBy(a => a.SendId)
            .Select(g => new { SendId = g.Key, Orders = g.Count(), Recipients = g.Select(a => a.DeliveryId).Distinct().Count(), Revenue = g.Sum(a => a.Revenue) })
            .ToListAsync(cancelToken).ConfigureAwait(false);
        foreach (var a in attribution)
        {
            result[a.SendId].Orders = a.Orders;
            result[a.SendId].OrderedRecipients = a.Recipients;
            result[a.SendId].Revenue = a.Revenue;
        }

        if (includeSkipReasons)
        {
            var reasons = await _dbContext.MarketingDelivery.AsNoTracking()
                .Where(d => sendIds.Contains(d.SendId) && d.Status == DeliveryStatus.Skipped && d.SkipReason != null)
                .GroupBy(d => new { d.SendId, d.SkipReason })
                .Select(g => new { g.Key.SendId, g.Key.SkipReason, Count = g.Count() })
                .ToListAsync(cancelToken).ConfigureAwait(false);
            foreach (var r in reasons)
                result[r.SendId].SkippedByReason[r.SkipReason!] = r.Count;
        }

        return result;
    }

    public class DeliveryRow
    {
        public MarketingDelivery Delivery { get; set; } = null!;
        public int? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public decimal? Revenue { get; set; }
        public DateTime? OrderCreatedAt { get; set; }
    }

    /// <summary>Recipients of a send for one of the result tabs: <c>ordered</c> | <c>clicked</c> | <c>failed</c> | <c>unsubscribed</c> | <c>skipped</c> | <c>all</c>.</summary>
    public async Task<(List<DeliveryRow> Items, int Total)> GetDeliveriesAsync(int sendId, string tab, int skip, int take, CancellationToken cancelToken, IReadOnlyCollection<int>? onlySiteIds = null)
    {
        var deliveries = _dbContext.MarketingDelivery.AsNoTracking().Where(d => d.SendId == sendId);
        if (onlySiteIds != null) { var ids = onlySiteIds.ToList(); deliveries = deliveries.Where(d => ids.Contains(d.SiteId)); }

        if (tab == "ordered")
        {
            var ordered =
                from a in _dbContext.MarketingAttribution.AsNoTracking().Where(a => a.SendId == sendId)
                join d in deliveries on a.DeliveryId equals d.Id
                join o in _dbContext.Order.AsNoTracking() on a.OrderId equals o.Id
                orderby a.OrderCreatedAt descending
                select new DeliveryRow { Delivery = d, OrderId = o.Id, OrderNumber = o.OrderNumber, Revenue = a.Revenue, OrderCreatedAt = a.OrderCreatedAt };
            var orderedTotal = await ordered.CountAsync(cancelToken).ConfigureAwait(false);
            var orderedItems = await ordered.Skip(skip).Take(take).ToListAsync(cancelToken).ConfigureAwait(false);
            return (orderedItems, orderedTotal);
        }

        deliveries = tab switch
        {
            "clicked" => deliveries.Where(d => d.ClickCount > 0).OrderByDescending(d => d.LastClickedAt),
            "failed" => deliveries.Where(d => d.Status == DeliveryStatus.Failed).OrderBy(d => d.Id),
            "unsubscribed" => deliveries.Where(d => d.UnsubscribedAt != null).OrderByDescending(d => d.UnsubscribedAt),
            "skipped" => deliveries.Where(d => d.Status == DeliveryStatus.Skipped).OrderBy(d => d.SkipReason).ThenBy(d => d.Id),
            _ => deliveries.OrderBy(d => d.Id),
        };

        var total = await deliveries.CountAsync(cancelToken).ConfigureAwait(false);
        var items = await deliveries.Skip(skip).Take(take)
            .Select(d => new DeliveryRow { Delivery = d })
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Cancel a send that has not finished: the send becomes <c>canceled</c> and whatever is still queued is skipped. Returns false when nothing could be canceled.</summary>
    /// <summary>Queued rows of a send that will never go out (materialization failed) - otherwise they keep counting toward the frequency cap.</summary>
    public async Task<int> SkipQueuedDeliveriesAsync(int sendId, string reason, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingDelivery
            .Where(d => d.SendId == sendId && d.Status == DeliveryStatus.Queued)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Status, DeliveryStatus.Skipped)
                .SetProperty(d => d.SkipReason, reason), cancelToken).ConfigureAwait(false);
    }

    /// <summary>An order cancelled after it was credited to a message stops counting as marketing revenue.</summary>
    public async Task<int> RemoveCanceledAttributionsAsync(CancellationToken cancelToken)
    {
        return await _dbContext.MarketingAttribution
            .Where(a => _dbContext.Order.Any(o => o.Id == a.OrderId && (o.Status == "Cancelled" || o.IsDeleted)))
            .ExecuteDeleteAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<string?> GetSiteUrlAsync(int siteId, CancellationToken cancelToken)
    {
        return await _dbContext.Set<Site>().AsNoTracking().Where(s => s.Id == siteId).Select(s => s.WooCommerceUrl).FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<bool> CancelSendAsync(int accountId, int sendId, CancellationToken cancelToken)
    {
        var changed = await _dbContext.MarketingSend
            .Where(s => s.AccountId == accountId && s.Id == sendId && (s.Status == SendStatus.Scheduled || s.Status == SendStatus.Sending))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, SendStatus.Canceled)
                .SetProperty(s => s.PausedReason, (string?)null)
                .SetProperty(s => s.CompletedAt, DateTime.UtcNow), cancelToken).ConfigureAwait(false);
        if (changed == 0)
            return false;

        await _dbContext.MarketingDelivery
            .Where(d => d.SendId == sendId && d.Status == DeliveryStatus.Queued)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Status, DeliveryStatus.Skipped)
                .SetProperty(d => d.SkipReason, SkipReason.Canceled), cancelToken).ConfigureAwait(false);
        return true;
    }


    //*************************    Dispatcher    *************************//

    public async Task<List<MarketingSend>> GetDueScheduledSendsAsync(DateTime utcNow, int take, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSend.AsNoTracking()
            .Where(s => s.Status == SendStatus.Scheduled && s.ScheduledAt <= utcNow)
            .OrderBy(s => s.ScheduledAt).Take(take)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<List<MarketingSend>> GetSendingSendsAsync(int take, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSend.AsNoTracking()
            .Where(s => s.Status == SendStatus.Sending)
            .OrderBy(s => s.StartedAt).Take(take)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Load-modify-save of one send. <paramref name="onlyWhenStatus"/> guards against overwriting a cancel that landed meanwhile.</summary>
    public async Task<bool> UpdateSendAsync(int sendId, string? onlyWhenStatus, Action<MarketingSend> apply, CancellationToken cancelToken)
    {
        var row = await _dbContext.MarketingSend.FirstOrDefaultAsync(s => s.Id == sendId, cancelToken).ConfigureAwait(false);
        if (row == null || (onlyWhenStatus != null && row.Status != onlyWhenStatus))
            return false;
        apply(row);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        _dbContext.Entry(row).State = EntityState.Detached;
        return true;
    }

    public class AudienceMember
    {
        public int CustomerId { get; set; }
        public int SiteId { get; set; }
        public string? Name { get; set; }
        public string NormalizedPhone { get; set; } = string.Empty;
        public bool HasConsent { get; set; }
    }

    public async Task<List<AudienceMember>> ResolveAudienceAsync(
        int accountId, IReadOnlyCollection<int> siteIds, IEnumerable<SegmentCondition>? conditions,
        DateTime utcNow, DateTime israelToday, CancellationToken cancelToken)
    {
        var query = await BuildAudienceQueryAsync(accountId, siteIds, conditions, utcNow, israelToday, cancelToken).ConfigureAwait(false);
        return await query
            .OrderBy(r => r.Customer.Id)
            .Select(r => new AudienceMember
            {
                CustomerId = r.Customer.Id,
                SiteId = r.Customer.SiteId,
                Name = r.Customer.Name,
                NormalizedPhone = r.Customer.NormalizedPhone,
                HasConsent = r.Customer.MarketingSms && r.Customer.OptedOutAt == null,
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Phones that already reached the frequency cap: <paramref name="capCount"/>+ marketing messages (sent or about to be) since <paramref name="sinceUtc"/>.</summary>
    public async Task<HashSet<string>> GetPhonesAtFrequencyCapAsync(int accountId, DateTime sinceUtc, int capCount, CancellationToken cancelToken)
    {
        var phones = await _dbContext.MarketingDelivery.AsNoTracking()
            .Where(d => d.AccountId == accountId
                && (((d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Sending) && d.CreationTime >= sinceUtc)
                    || ((d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Delivered) && d.SentAt >= sinceUtc)))
            .GroupBy(d => d.NormalizedPhone)
            .Where(g => g.Count() >= capCount)
            .Select(g => g.Key)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return phones.ToHashSet();
    }

    /// <summary>Phones that already placed an order since <paramref name="sinceUtc"/> (Israel midnight) - no promo for someone who bought today.</summary>
    public async Task<HashSet<string>> GetPhonesOrderedSinceAsync(int accountId, DateTime sinceUtc, CancellationToken cancelToken)
    {
        var phones = await _dbContext.Order.AsNoTracking()
            .Where(o => o.AccountId == accountId && !o.IsDeleted && o.Status != "Cancelled" && o.CreationTime >= sinceUtc && o.CustomerId != null)
            .Select(o => o.Customer!.NormalizedPhone)
            .Distinct()
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return phones.Where(p => !string.IsNullOrEmpty(p)).ToHashSet();
    }

    public async Task<HashSet<string>> GetExistingTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancelToken)
    {
        var existing = await _dbContext.MarketingDelivery.AsNoTracking()
            .Where(d => d.ShortToken != null && tokens.Contains(d.ShortToken))
            .Select(d => d.ShortToken!)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return existing.ToHashSet();
    }

    public async Task AddDeliveriesAsync(IReadOnlyList<MarketingDelivery> deliveries, CancellationToken cancelToken)
    {
        const int chunk = 1000;
        for (var i = 0; i < deliveries.Count; i += chunk)
        {
            _dbContext.MarketingDelivery.AddRange(deliveries.Skip(i).Take(chunk));
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            _dbContext.ClearCache();
        }
    }

    /// <summary>
    /// Claims the next batch of queued deliveries (queued → sending, stamped with the claim time in SentAt).
    /// Assumes a single dispatcher instance, like the other hosted services of this API.
    /// </summary>
    public async Task<List<MarketingDelivery>> ClaimQueuedDeliveriesAsync(int sendId, int take, CancellationToken cancelToken)
    {
        var ids = await _dbContext.MarketingDelivery.AsNoTracking()
            .Where(d => d.SendId == sendId && d.Status == DeliveryStatus.Queued)
            .OrderBy(d => d.Id).Take(take).Select(d => d.Id)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        if (ids.Count == 0)
            return new List<MarketingDelivery>();

        var now = DateTime.UtcNow;
        await _dbContext.MarketingDelivery
            .Where(d => ids.Contains(d.Id) && d.Status == DeliveryStatus.Queued)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, DeliveryStatus.Sending).SetProperty(d => d.SentAt, now), cancelToken)
            .ConfigureAwait(false);

        return await _dbContext.MarketingDelivery.AsNoTracking()
            .Where(d => ids.Contains(d.Id) && d.Status == DeliveryStatus.Sending)
            .OrderBy(d => d.Id)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task MarkDeliveryResultAsync(long deliveryId, bool success, int costUnits, string? error, CancellationToken cancelToken)
    {
        var status = success ? DeliveryStatus.Sent : DeliveryStatus.Failed;
        var now = DateTime.UtcNow;
        var err = error != null && error.Length > 500 ? error.Substring(0, 500) : error;
        await _dbContext.MarketingDelivery
            .Where(d => d.Id == deliveryId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Status, status)
                .SetProperty(d => d.CostUnits, costUnits)
                .SetProperty(d => d.SentAt, now)
                .SetProperty(d => d.Error, err), cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provider delivery report for one message. Only a delivery we sent can be reported on; a report that arrives
    /// twice (the provider retries) is a no-op the second time.
    /// </summary>
    public async Task<bool> ApplyDeliveryReportAsync(long deliveryId, bool delivered, string? error, DateTime at, CancellationToken cancelToken)
    {
        var changed = delivered
            ? await _dbContext.MarketingDelivery
                .Where(d => d.Id == deliveryId && d.Status == DeliveryStatus.Sent)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(d => d.Status, DeliveryStatus.Delivered)
                    .SetProperty(d => d.DeliveredAt, at)
                    .SetProperty(d => d.Error, (string?)null), cancelToken).ConfigureAwait(false)
            // A late/duplicate negative report must not undo a confirmed delivery: only sent → failed.
            : await _dbContext.MarketingDelivery
                .Where(d => d.Id == deliveryId && d.Status == DeliveryStatus.Sent)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(d => d.Status, DeliveryStatus.Failed)
                    .SetProperty(d => d.DeliveredAt, (DateTime?)null)
                    .SetProperty(d => d.Error, error), cancelToken).ConfigureAwait(false);
        return changed > 0;
    }

    /// <summary>Returns claimed-but-unsent deliveries to the queue (e.g. the quota ran out mid-batch).</summary>
    public async Task ReleaseDeliveriesAsync(IReadOnlyCollection<long> deliveryIds, CancellationToken cancelToken)
    {
        if (deliveryIds.Count == 0)
            return;
        await _dbContext.MarketingDelivery
            .Where(d => deliveryIds.Contains(d.Id) && d.Status == DeliveryStatus.Sending)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, DeliveryStatus.Queued).SetProperty(d => d.SentAt, (DateTime?)null), cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A delivery stuck in <c>sending</c> means the process died mid-send. It is marked failed rather than
    /// re-queued: we cannot know whether the SMS left, and a double marketing SMS is worse than a missing one.
    /// </summary>
    public async Task<int> FailStaleSendingDeliveriesAsync(DateTime olderThanUtc, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingDelivery
            .Where(d => d.Status == DeliveryStatus.Sending && d.SentAt != null && d.SentAt < olderThanUtc)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Status, DeliveryStatus.Failed)
                .SetProperty(d => d.Error, "השליחה נותקה באמצע - לא נשלח שוב כדי לא לשלוח פעמיים"), cancelToken).ConfigureAwait(false);
    }

    public async Task<bool> HasQueuedDeliveriesAsync(int sendId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingDelivery.AsNoTracking()
            .AnyAsync(d => d.SendId == sendId && (d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Sending), cancelToken)
            .ConfigureAwait(false);
    }


    //*************************    Quota ledger    *************************//

    public async Task<int> GetBankBalanceAsync(int accountId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingQuotaLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId && l.Bucket == BucketBank)
            .SumAsync(l => (int?)l.Amount, cancelToken).ConfigureAwait(false) ?? 0;
    }

    public async Task AddLedgerEntryAsync(MarketingQuotaLedger entry, CancellationToken cancelToken)
    {
        _dbContext.MarketingQuotaLedger.Add(entry);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<List<MarketingQuotaLedger>> GetLedgerAsync(int accountId, int take, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingQuotaLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .OrderByDescending(l => l.Id).Take(take)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public class QuotaTotals
    {
        public int Purchased { get; set; }
        public int Consumed { get; set; }
        public DateTime? LastPurchaseAt { get; set; }
        public int ConsumedLast60Days { get; set; }
    }

    public async Task<QuotaTotals> GetQuotaTotalsAsync(int accountId, DateTime utcNow, CancellationToken cancelToken)
    {
        var rows = await _dbContext.MarketingQuotaLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId && l.Bucket == BucketBank)
            .Select(l => new { l.EntryType, l.Amount, l.CreationTime })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var since = utcNow.AddDays(-60);
        return new QuotaTotals
        {
            Purchased = rows.Where(r => r.Amount > 0).Sum(r => r.Amount),
            Consumed = -rows.Where(r => r.Amount < 0).Sum(r => r.Amount),
            LastPurchaseAt = rows.Where(r => r.Amount > 0).Select(r => (DateTime?)r.CreationTime).Max(),
            ConsumedLast60Days = -rows.Where(r => r.Amount < 0 && r.CreationTime >= since).Sum(r => r.Amount),
        };
    }


    //*************************    Attribution    *************************//

    public class AttributionCandidate
    {
        public int OrderId { get; set; }
        public int AccountId { get; set; }
        public int? CustomerId { get; set; }
        public decimal Revenue { get; set; }
        public DateTime OrderCreatedAt { get; set; }
        public long DeliveryId { get; set; }
        public int SendId { get; set; }
        public int WindowHours { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? LastClickedAt { get; set; }
    }

    /// <summary>
    /// Orders not yet attributed whose customer phone received a marketing message inside that send's
    /// attribution window before the order. Driven from deliveries, so it costs nothing while nobody is sending.
    /// </summary>
    public async Task<List<AttributionCandidate>> GetAttributionCandidatesAsync(DateTime deliveriesSinceUtc, CancellationToken cancelToken)
    {
        var query =
            from d in _dbContext.MarketingDelivery.AsNoTracking()
            where (d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Delivered) && d.SentAt >= deliveriesSinceUtc
            join s in _dbContext.MarketingSend.AsNoTracking() on d.SendId equals s.Id
            join c in _dbContext.Set<Customer>().AsNoTracking() on new { d.AccountId, d.NormalizedPhone } equals new { c.AccountId, c.NormalizedPhone }
            join o in _dbContext.Order.AsNoTracking() on c.Id equals o.CustomerId
            where !o.IsDeleted && o.Status != "Cancelled"
                && o.CreationTime >= d.SentAt
                && o.CreationTime <= d.SentAt!.Value.AddHours(s.AttributionWindowHours)
                && !_dbContext.MarketingAttribution.Any(a => a.OrderId == o.Id)
            select new AttributionCandidate
            {
                OrderId = o.Id,
                AccountId = o.AccountId,
                CustomerId = o.CustomerId,
                Revenue = o.Total ?? 0m,
                OrderCreatedAt = o.CreationTime,
                DeliveryId = d.Id,
                SendId = d.SendId,
                WindowHours = s.AttributionWindowHours,
                SentAt = d.SentAt!.Value,
                LastClickedAt = d.LastClickedAt,
            };
        return await query.ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task AddAttributionsAsync(IReadOnlyCollection<MarketingAttribution> rows, CancellationToken cancelToken)
    {
        if (rows.Count == 0)
            return;
        _dbContext.MarketingAttribution.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }


    //*************************    Public token endpoints    *************************//

    public async Task<MarketingDelivery?> GetDeliveryByTokenAsync(string token, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingDelivery.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ShortToken == token, cancelToken).ConfigureAwait(false);
    }

    public async Task<string?> GetSendLinkUrlAsync(int sendId, CancellationToken cancelToken)
    {
        return await _dbContext.MarketingSend.AsNoTracking()
            .Where(s => s.Id == sendId).Select(s => s.LinkUrl)
            .FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Carrier/antivirus link scanners open every link within seconds of delivery - a hit that early is not a person.</summary>
    public static readonly TimeSpan ScannerClickGrace = TimeSpan.FromSeconds(10);

    public async Task RegisterClickAsync(long deliveryId, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var humanAfter = now - ScannerClickGrace;
        await _dbContext.MarketingDelivery
            .Where(d => d.Id == deliveryId && (d.SentAt == null || d.SentAt <= humanAfter))
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.ClickCount, d => d.ClickCount + 1)
                .SetProperty(d => d.FirstClickedAt, d => d.FirstClickedAt ?? now)
                .SetProperty(d => d.LastClickedAt, now), cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unified opt-out (spec §07): every customer row of the account with this phone is removed from
    /// marketing - the same person at another branch must not keep receiving messages. Idempotent.
    /// </summary>
    /// <summary>When this phone opted out anywhere in the account (null = still subscribed).</summary>
    public async Task<DateTime?> GetOptOutAtAsync(int accountId, string? normalizedPhone, CancellationToken cancelToken)
    {
        if (string.IsNullOrEmpty(normalizedPhone))
            return null;
        return await _dbContext.Set<Customer>().AsNoTracking()
            .Where(c => c.AccountId == accountId && c.NormalizedPhone == normalizedPhone && c.OptedOutAt != null)
            .MinAsync(c => c.OptedOutAt, cancelToken).ConfigureAwait(false);
    }

    public async Task<int> OptOutByPhoneAsync(int accountId, string normalizedPhone, string source, long? deliveryId, CancellationToken cancelToken)
    {
        if (string.IsNullOrEmpty(normalizedPhone))
            return 0;
        var now = DateTime.UtcNow;
        var changed = await _dbContext.Set<Customer>()
            .Where(c => c.AccountId == accountId && c.NormalizedPhone == normalizedPhone && c.OptedOutAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.MarketingSms, false)
                .SetProperty(c => c.MarketingApproval, false)
                .SetProperty(c => c.OptedOutAt, now)
                .SetProperty(c => c.OptedOutSource, source)
                .SetProperty(c => c.OptedOutDeliveryId, deliveryId)
                .SetProperty(c => c.UpdatedDate, now), cancelToken).ConfigureAwait(false);

        if (deliveryId.HasValue)
        {
            await _dbContext.MarketingDelivery
                .Where(d => d.Id == deliveryId.Value && d.UnsubscribedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(d => d.UnsubscribedAt, now), cancelToken).ConfigureAwait(false);
        }

        // Anything still waiting in a queue for this phone must not go out.
        await _dbContext.MarketingDelivery
            .Where(d => d.AccountId == accountId && d.NormalizedPhone == normalizedPhone && d.Status == DeliveryStatus.Queued)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Status, DeliveryStatus.Skipped)
                .SetProperty(d => d.SkipReason, SkipReason.NoConsent), cancelToken).ConfigureAwait(false);

        return changed;
    }


    //*************************    Message log    *************************//

    public async Task AddMessageLogsAsync(IReadOnlyCollection<MessageLog> rows, CancellationToken cancelToken)
    {
        if (rows.Count == 0)
            return;
        _dbContext.MessageLog.AddRange(rows);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    public class UsageRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Units { get; set; }
        public int Messages { get; set; }
    }

    /// <summary>What actually went out, per month / kind / category - the "what do I need" graph (spec §8.3).</summary>
    public async Task<List<UsageRow>> GetUsageAsync(int accountId, DateTime fromUtc, CancellationToken cancelToken)
    {
        return await _dbContext.MessageLog.AsNoTracking()
            .Where(m => m.AccountId == accountId && m.Success && m.CreationTime >= fromUtc)
            .GroupBy(m => new { m.CreationTime.Year, m.CreationTime.Month, m.Kind, m.Category })
            .Select(g => new UsageRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Kind = g.Key.Kind,
                Category = g.Key.Category,
                Units = g.Sum(m => m.Units),
                Messages = g.Count(),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<int, string>> GetSiteNamesAsync(int accountId, CancellationToken cancelToken)
    {
        return await _dbContext.Set<Site>().AsNoTracking()
            .Where(s => s.AccountId == accountId)
            .ToDictionaryAsync(s => s.Id, s => s.SiteName ?? string.Empty, cancelToken).ConfigureAwait(false);
    }
}
