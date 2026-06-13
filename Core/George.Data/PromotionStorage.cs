using George.Common;
using George.Data.Models;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class PromotionListRow
{
    public Promotion Promotion { get; set; } = null!;
    public int PeriodRedemptions { get; set; }
    public decimal PeriodRevenueNis { get; set; }
    public decimal PeriodDiscountNis { get; set; }
}

public class PromotionStorage : StorageBase
{
    public PromotionStorage(GeorgeDBContext dbContext, ILogger<PromotionStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<DataListResult<PromotionListRow>> GetPromotionsAsync(
        PromotionFilter filter,
        PagingExDto paging,
        CancellationToken cancelToken)
    {
        var res = new DataListResult<PromotionListRow>();
        var utcNow = DateTime.UtcNow.Date;
        var fromDay = filter.PeriodFromUtc?.Date;
        var toDay = filter.PeriodToUtc?.Date;

        IReadOnlyList<int>? searchProductIds = null;
        IReadOnlyList<int>? searchCategoryIds = null;
        if (filter.Search?.SearchTerm.HasValue() == true)
        {
            var term = filter.Search!.SearchTerm!.Trim();
            searchProductIds = await ResolveSearchProductIdsAsync(filter.SiteId, term, cancelToken).ConfigureAwait(false);
            searchCategoryIds = await ResolveSearchCategoryIdsAsync(filter.SiteId, term, cancelToken).ConfigureAwait(false);
        }

        var metricAgg = BuildMetricAggregationQuery(fromDay, toDay, filter.Channel);

        var basePromotions = ApplyPromotionFilters(_dbContext.Promotion.AsNoTracking(), filter, utcNow, searchProductIds, searchCategoryIds);

        var joined =
            from p in basePromotions
            join g in metricAgg on p.Id equals g.PromotionId into gj
            from g in gj.DefaultIfEmpty()
            select new PromotionListRow
            {
                Promotion = p,
                // Left-join null metrics (e.g. drafts) must coalesce in SQL — non-nullable reads throw.
                PeriodRedemptions = (int?)g.Redemptions ?? 0,
                PeriodRevenueNis = (decimal?)g.RevenueNis ?? 0m,
                PeriodDiscountNis = (decimal?)g.DiscountNis ?? 0m,
            };

        joined = ApplyPromotionSort(joined, filter);

        if (paging.IncludeTotal)
            res.Total = await joined.CountAsync(cancelToken).ConfigureAwait(false);

        res.Items = await joined
            .Skip(paging.Skip)
            .Take(paging.Take)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        return res;
    }

    /// <summary>Tab totals for badges (site-wide, not affected by search/type/channel).</summary>
    public async Task<PromotionTabCounts> GetPromotionTabCountsAsync(int siteId, CancellationToken cancelToken)
    {
        var utcNow = DateTime.UtcNow.Date;
        var q = _dbContext.Promotion.AsNoTracking().Where(p => p.SiteId == siteId);

        var all = await q.CountAsync(cancelToken).ConfigureAwait(false);
        var drafts = await q.Where(p => p.IsDraft).CountAsync(cancelToken).ConfigureAwait(false);
        var active = await q.Where(p => !p.IsDraft && p.IsActive
            && (p.ScheduleStartDateUtc == null || p.ScheduleStartDateUtc <= utcNow)
            && (p.ScheduleEndDateUtc == null || p.ScheduleEndDateUtc >= utcNow)).CountAsync(cancelToken).ConfigureAwait(false);
        var scheduled = await q.Where(p => !p.IsDraft && p.ScheduleStartDateUtc != null && p.ScheduleStartDateUtc > utcNow)
            .CountAsync(cancelToken).ConfigureAwait(false);
        var ended = await q.Where(p => !p.IsDraft && p.ScheduleEndDateUtc != null && p.ScheduleEndDateUtc < utcNow)
            .CountAsync(cancelToken).ConfigureAwait(false);

        return new PromotionTabCounts
        {
            All = all,
            Active = active,
            Scheduled = scheduled,
            Drafts = drafts,
            Ended = ended,
        };
    }

    /// <summary>Active promotions whose end date falls within the next 7 days (inclusive).</summary>
    public async Task<int> GetActivePromotionsEndingWithinWeekAsync(int siteId, CancellationToken cancelToken)
    {
        var utcNow = DateTime.UtcNow.Date;
        var weekEnd = utcNow.AddDays(7);
        return await _dbContext.Promotion.AsNoTracking()
            .Where(p => p.SiteId == siteId)
            .Where(p => !p.IsDraft && p.IsActive
                && (p.ScheduleStartDateUtc == null || p.ScheduleStartDateUtc <= utcNow)
                && (p.ScheduleEndDateUtc == null || p.ScheduleEndDateUtc >= utcNow))
            .Where(p => p.ScheduleEndDateUtc != null
                && p.ScheduleEndDateUtc >= utcNow
                && p.ScheduleEndDateUtc <= weekEnd)
            .CountAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sum daily metrics for promotions matching KPI filters (period + optional channel/discount kind).</summary>
    public async Task<PromotionPeriodTotals> GetPromotionPeriodTotalsAsync(
        int siteId,
        DateTime? periodFromUtc,
        DateTime? periodToUtc,
        string? channel,
        string? discountKind,
        CancellationToken cancelToken)
    {
        var fromDay = periodFromUtc?.Date;
        var toDay = periodToUtc?.Date;

        // Channel filter applies to order source on metrics rows, not Promotion.ChannelsJson.
        var promoQuery = ApplyKpiPromotionFilters(
            _dbContext.Promotion.AsNoTracking().Where(p => p.SiteId == siteId),
            channel: null,
            discountKind);

        var ids = await promoQuery.Select(p => p.Id).ToListAsync(cancelToken).ConfigureAwait(false);
        if (ids.Count == 0)
            return new PromotionPeriodTotals();

        var mq = _dbContext.PromotionDailyMetric.AsNoTracking().Where(m => ids.Contains(m.PromotionId));
        if (!string.IsNullOrWhiteSpace(channel)
            && !channel.Equals(PromotionWire.Channel.All, StringComparison.OrdinalIgnoreCase))
        {
            var ch = NormalizeMetricChannel(channel);
            mq = mq.Where(m => m.Channel == ch);
        }
        if (fromDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc >= fromDay.Value);
        if (toDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc <= toDay.Value);

        var redemptions = await mq.SumAsync(m => m.RedemptionsCount, cancelToken).ConfigureAwait(false);
        var revenue = await mq.SumAsync(m => m.RevenueNis, cancelToken).ConfigureAwait(false);
        var discount = await mq.SumAsync(m => m.DiscountNis, cancelToken).ConfigureAwait(false);

        return new PromotionPeriodTotals
        {
            Redemptions = redemptions,
            RevenueNis = revenue,
            DiscountNis = discount,
        };
    }

    public async Task<decimal> GetSiteOrderTotalForPeriodAsync(
        int siteId,
        DateTime periodStartInclusiveUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancelToken)
    {
        return await _dbContext.Order.AsNoTracking()
            .Where(o => o.SiteId == siteId && !o.IsDeleted && o.Status != "Cancelled")
            .Where(o => o.CreationTime >= periodStartInclusiveUtc && o.CreationTime < periodEndExclusiveUtc)
            .SumAsync(o => o.Total ?? 0m, cancelToken)
            .ConfigureAwait(false);
    }

    public async Task<Promotion?> GetPromotionAsync(int promotionId, CancellationToken cancelToken)
    {
        return await _dbContext.Promotion
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Promotions whose end-date has passed but are still <c>IsActive=true</c>.
    /// Used by the midnight expiry job to flip them off and emit <c>promotion.ended</c>.
    /// Spec: <c>Sprint4/מבצעים.md</c> "סיום מבצע אוטומטי".
    /// </summary>
    public async Task<List<Promotion>> GetExpiredActivePromotionsAsync(
        DateTime utcNow, CancellationToken cancelToken)
    {
        var today = utcNow.Date;
        return await _dbContext.Promotion
            .AsNoTracking()
            .Where(p => !p.IsDeleted
                && !p.IsDraft
                && p.IsActive
                && p.ScheduleEndDateUtc != null
                && p.ScheduleEndDateUtc!.Value.Date < today)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Promotions eligible for cart-time evaluation: not deleted, not draft, IsActive,
    /// and inside the optional date window. Used by <c>POST /Promotion/evaluate</c>.
    /// </summary>
    public async Task<List<Promotion>> GetActivePromotionsForEvaluationAsync(
        int siteId, DateTime utcNow, CancellationToken cancelToken)
    {
        var today = utcNow.Date;
        return await _dbContext.Promotion
            .AsNoTracking()
            .Where(p => p.SiteId == siteId
                && !p.IsDeleted
                && !p.IsDraft
                && p.IsActive
                && (p.ScheduleStartDateUtc == null || p.ScheduleStartDateUtc.Value.Date <= today)
                && (p.ScheduleEndDateUtc == null || p.ScheduleEndDateUtc.Value.Date >= today))
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Increment per-promotion daily aggregates from order lines stamped at finalize.
    /// Spec: <c>Sprint4/מבצעים.md</c> — מימושים / הכנסות / הנחות.
    /// One redemption per order per promotion; revenue and discount summed across matching lines.
    /// </summary>
    public Task RecordPromotionMetricsFromOrderAsync(
        DateTime orderCreationTimeUtc,
        string? orderChannel,
        IReadOnlyList<OrderItem> items,
        CancellationToken cancelToken) =>
        ApplyPromotionMetricsDeltaAsync(orderCreationTimeUtc, orderChannel, items, redemptionDelta: 1, cancelToken);

    /// <summary>Undo <see cref="RecordPromotionMetricsFromOrderAsync"/> when an order is cancelled.</summary>
    public Task ReversePromotionMetricsFromOrderAsync(
        DateTime orderCreationTimeUtc,
        string? orderChannel,
        IReadOnlyList<OrderItem> items,
        CancellationToken cancelToken) =>
        ApplyPromotionMetricsDeltaAsync(orderCreationTimeUtc, orderChannel, items, redemptionDelta: -1, cancelToken);

    /// <summary>
    /// RevenueNis = sum of net line totals (TotalPrice − DiscountAmount) on promoted lines.
    /// DiscountNis = sum of promotion discounts. Yield KPI = RevenueNis / DiscountNis.
    /// </summary>
    private async Task ApplyPromotionMetricsDeltaAsync(
        DateTime orderCreationTimeUtc,
        string? orderChannel,
        IReadOnlyList<OrderItem> items,
        int redemptionDelta,
        CancellationToken cancelToken)
    {
        if (items == null || items.Count == 0)
            return;

        var channel = NormalizeMetricChannel(orderChannel);
        var groups = items
            .Where(i => i.PromotionId is > 0)
            .GroupBy(i => i.PromotionId!.Value)
            .Select(g => new
            {
                PromotionId = g.Key,
                RevenueNis = g.Sum(x => (x.TotalPrice ?? 0m) - (x.DiscountAmount ?? 0m)),
                DiscountNis = g.Sum(x => x.DiscountAmount ?? 0m),
            })
            .ToList();

        if (groups.Count == 0)
            return;

        var metricDate = DateTime.SpecifyKind(orderCreationTimeUtc.Date, DateTimeKind.Utc);
        var promoIds = groups.Select(g => g.PromotionId).ToList();

        var existing = await _dbContext.PromotionDailyMetric
            .Where(m => m.MetricDateUtc == metricDate && m.Channel == channel && promoIds.Contains(m.PromotionId))
            .ToDictionaryAsync(m => m.PromotionId, cancelToken)
            .ConfigureAwait(false);

        foreach (var g in groups)
        {
            if (!existing.TryGetValue(g.PromotionId, out var row))
            {
                if (redemptionDelta < 0)
                    continue;
                row = new PromotionDailyMetric
                {
                    PromotionId = g.PromotionId,
                    MetricDateUtc = metricDate,
                    Channel = channel,
                };
                _dbContext.PromotionDailyMetric.Add(row);
                existing[g.PromotionId] = row;
            }

            row.RedemptionsCount = Math.Max(0, row.RedemptionsCount + redemptionDelta);
            row.RevenueNis = Math.Max(0m, row.RevenueNis + redemptionDelta * g.RevenueNis);
            row.DiscountNis = Math.Max(0m, row.DiscountNis + redemptionDelta * g.DiscountNis);
        }

        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Increment daily KPI from WooCommerce / Promeng outbound redemption reports (GIORGIO_API §2.1).
    /// </summary>
    public async Task<int> RecordExternalRedemptionsAsync(
        int siteId,
        IReadOnlyList<(int PromotionId, decimal DiscountAmount, DateTime RedeemedAtUtc, string Channel)> rows,
        CancellationToken cancelToken)
    {
        if (rows == null || rows.Count == 0) return 0;

        var promoIds = rows.Select(r => r.PromotionId).Distinct().ToList();
        var validIds = await _dbContext.Promotion.AsNoTracking()
            .Where(p => p.SiteId == siteId && !p.IsDeleted && promoIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        var validSet = validIds.ToHashSet();

        var recorded = 0;
        foreach (var group in rows.Where(r => validSet.Contains(r.PromotionId)).GroupBy(r => new
        {
            r.PromotionId,
            MetricDate = DateTime.SpecifyKind(r.RedeemedAtUtc.Date, DateTimeKind.Utc),
            Channel = NormalizeMetricChannel(r.Channel),
        }))
        {
            var discountSum = group.Sum(g => Math.Max(0m, g.DiscountAmount));
            var count = group.Count();

            var row = await _dbContext.PromotionDailyMetric
                .FirstOrDefaultAsync(
                    m => m.PromotionId == group.Key.PromotionId
                         && m.MetricDateUtc == group.Key.MetricDate
                         && m.Channel == group.Key.Channel,
                    cancelToken)
                .ConfigureAwait(false);

            if (row == null)
            {
                row = new PromotionDailyMetric
                {
                    PromotionId = group.Key.PromotionId,
                    MetricDateUtc = group.Key.MetricDate,
                    Channel = group.Key.Channel,
                };
                _dbContext.PromotionDailyMetric.Add(row);
            }

            row.RedemptionsCount = Math.Max(0, row.RedemptionsCount + count);
            row.DiscountNis = Math.Max(0m, row.DiscountNis + discountSum);
            recorded += count;
        }

        if (recorded > 0)
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return recorded;
    }

    internal static string NormalizeMetricChannel(string? orderChannel)
    {
        var c = (orderChannel ?? "").Trim().ToLowerInvariant();
        return c switch
        {
            PromotionWire.Channel.Store => PromotionWire.Channel.Store,
            PromotionWire.Channel.Mobile => PromotionWire.Channel.Mobile,
            PromotionWire.Channel.Phone => PromotionWire.Channel.Phone,
            PromotionWire.Channel.Web => PromotionWire.Channel.Web,
            "" => PromotionWire.Channel.Web,
            _ => c.Length <= 20 ? c : PromotionWire.Channel.Web,
        };
    }

    /// <summary>
    /// Counts prior redemptions per promotion for a customer — distinct orders with a stamped
    /// <see cref="OrderItem.PromotionId"/>. Used to enforce <c>limits.perCustomer</c> at evaluate time.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, int>> GetCustomerPromotionRedemptionCountsAsync(
        int siteId,
        int customerId,
        IReadOnlyCollection<int> promotionIds,
        CancellationToken cancelToken)
    {
        if (promotionIds == null || promotionIds.Count == 0)
            return new Dictionary<int, int>();

        var ids = promotionIds.Distinct().ToList();
        var rows = await (
            from oi in _dbContext.OrderItem.AsNoTracking()
            join o in _dbContext.Order.AsNoTracking() on oi.OrderId equals o.Id
            where o.SiteId == siteId
                  && o.CustomerId == customerId
                  && !o.IsDeleted
                  && o.Status != "Cancelled"
                  && oi.PromotionId != null
                  && ids.Contains(oi.PromotionId.Value)
            group oi by oi.PromotionId into g
            select new
            {
                PromotionId = g.Key!.Value,
                OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
            })
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.PromotionId, r => r.OrderCount);
    }

    public async Task<Promotion> CreatePromotionAsync(Promotion entity, CancellationToken cancelToken)
    {
        _dbContext.Promotion.Add(entity);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<Promotion?> UpdatePromotionAsync(int promotionId, Action<Promotion> apply, CancellationToken cancelToken)
    {
        var db = await _dbContext.Promotion
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancelToken)
            .ConfigureAwait(false);
        if (db == null) return null;
        apply(db);
        db.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return db;
    }

    public async Task<bool> DeletePromotionAsync(int promotionId, CancellationToken cancelToken)
    {
        var db = await _dbContext.Promotion
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancelToken)
            .ConfigureAwait(false);
        if (db == null) return false;
        _dbContext.Promotion.Remove(db);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Case-insensitive match on coupon code within a site (ignores null/empty codes).</summary>
    public async Task<bool> PromotionCouponCodeExistsAsync(
        int siteId,
        string normalizedCoupon,
        int? excludePromotionId,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedCoupon))
            return false;

        var q = _dbContext.Promotion.AsNoTracking()
            .Where(p => p.SiteId == siteId && p.CouponCode != null && p.CouponCode.ToLower() == normalizedCoupon);
        if (excludePromotionId.HasValue)
            q = q.Where(p => p.Id != excludePromotionId.Value);
        return await q.AnyAsync(cancelToken).ConfigureAwait(false);
    }

    private IQueryable<MetricAgg> BuildMetricAggregationQuery(DateTime? fromDay, DateTime? toDay, string? orderChannel)
    {
        var mq = _dbContext.PromotionDailyMetric.AsNoTracking();
        if (fromDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc >= fromDay.Value);
        if (toDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc <= toDay.Value);
        if (!string.IsNullOrWhiteSpace(orderChannel)
            && !orderChannel.Equals(PromotionWire.Channel.All, StringComparison.OrdinalIgnoreCase))
        {
            var ch = NormalizeMetricChannel(orderChannel);
            mq = mq.Where(m => m.Channel == ch);
        }

        return mq.GroupBy(m => m.PromotionId).Select(g => new MetricAgg
        {
            PromotionId = g.Key,
            Redemptions = g.Sum(x => x.RedemptionsCount),
            RevenueNis = g.Sum(x => x.RevenueNis),
            DiscountNis = g.Sum(x => x.DiscountNis),
        });
    }

    private sealed class MetricAgg
    {
        public int PromotionId { get; set; }
        public int Redemptions { get; set; }
        public decimal RevenueNis { get; set; }
        public decimal DiscountNis { get; set; }
    }

    private async Task<List<int>> ResolveSearchProductIdsAsync(int siteId, string term, CancellationToken cancelToken) =>
        await _dbContext.Product.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Name.Contains(term))
            .Where(p => p.Site.Any(s => s.Id == siteId))
            .Select(p => p.Id)
            .Take(100)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

    private async Task<List<int>> ResolveSearchCategoryIdsAsync(int siteId, string term, CancellationToken cancelToken) =>
        await _dbContext.Category.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Name.Contains(term))
            .Where(c => c.Site.Any(s => s.Id == siteId))
            .Select(c => c.Id)
            .Take(100)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);

    private static IQueryable<Promotion> ApplyPromotionFilters(
        IQueryable<Promotion> query,
        PromotionFilter filter,
        DateTime utcNowDate,
        IReadOnlyList<int>? searchProductIds = null,
        IReadOnlyList<int>? searchCategoryIds = null)
    {
        query = query.Where(p => p.SiteId == filter.SiteId);

        if (!string.IsNullOrWhiteSpace(filter.PromotionType))
        {
            var t = filter.PromotionType.Trim();
            query = query.Where(p => p.PromotionType == t);
        }

        query = ApplyListTab(query, filter.ListTab, utcNowDate);

        if (filter.Search?.SearchTerm.HasValue() == true)
        {
            var term = filter.Search!.SearchTerm!.Trim();
            var productPatterns = BuildPayloadIdPatterns(searchProductIds, "productId", "productIds");
            var categoryPatterns = BuildPayloadIdPatterns(searchCategoryIds, "categoryId", "categoryIds");
            query = query.Where(p =>
                p.Name.Contains(term)
                || p.PayloadJson.Contains(term)
                || (p.CouponCode != null && p.CouponCode.Contains(term))
                || (p.AppliesToSummary != null && p.AppliesToSummary.Contains(term))
                || productPatterns.Any(pat => p.PayloadJson.Contains(pat))
                || categoryPatterns.Any(pat => p.PayloadJson.Contains(pat)));
        }

        if (!string.IsNullOrWhiteSpace(filter.DiscountKind)
            && !filter.DiscountKind.Equals(PromotionWire.DiscountKind.All, StringComparison.OrdinalIgnoreCase))
        {
            var dk = filter.DiscountKind.Trim().ToLowerInvariant();
            query = query.Where(p => p.ListDiscountKind.ToLower() == dk);
        }

        // Channel filter is applied on PromotionDailyMetric.Channel (order source), not Promotion.ChannelsJson.

        return query;
    }

    private static IQueryable<Promotion> ApplyKpiPromotionFilters(IQueryable<Promotion> query, string? channel, string? discountKind)
    {
        if (!string.IsNullOrWhiteSpace(discountKind)
            && !discountKind.Equals(PromotionWire.DiscountKind.All, StringComparison.OrdinalIgnoreCase))
        {
            var dk = discountKind.Trim().ToLowerInvariant();
            query = query.Where(p => p.ListDiscountKind.ToLower() == dk);
        }

        if (!string.IsNullOrWhiteSpace(channel)
            && !channel.Equals(PromotionWire.Channel.All, StringComparison.OrdinalIgnoreCase))
        {
            var token = "\"" + channel.Trim().ToLowerInvariant() + "\"";
            query = query.Where(p => (p.ChannelsJson ?? PromotionWire.DefaultChannelsJson)!.Contains(token));
        }

        return query;
    }

    private static IQueryable<Promotion> ApplyListTab(IQueryable<Promotion> query, string? listTab, DateTime utcNowDate)
    {
        var tab = listTab?.Trim().ToLowerInvariant();
        return tab switch
        {
            PromotionWire.ListTab.Drafts => query.Where(p => p.IsDraft),
            PromotionWire.ListTab.Active => query.Where(p => !p.IsDraft && p.IsActive
                && (p.ScheduleStartDateUtc == null || p.ScheduleStartDateUtc <= utcNowDate)
                && (p.ScheduleEndDateUtc == null || p.ScheduleEndDateUtc >= utcNowDate)),
            PromotionWire.ListTab.Scheduled => query.Where(p => !p.IsDraft && p.ScheduleStartDateUtc != null && p.ScheduleStartDateUtc > utcNowDate),
            PromotionWire.ListTab.Ended => query.Where(p => !p.IsDraft && p.ScheduleEndDateUtc != null && p.ScheduleEndDateUtc < utcNowDate),
            _ => query,
        };
    }

    private static IQueryable<PromotionListRow> ApplyPromotionSort(IQueryable<PromotionListRow> query, PromotionFilter filter)
    {
        var sortBy = (filter.SortBy ?? PromotionWire.SortBy.Updated).Trim().ToLowerInvariant();
        var desc = string.IsNullOrWhiteSpace(filter.SortDir)
            || filter.SortDir.Equals(PromotionWire.SortDir.Desc, StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            PromotionWire.SortBy.Redemptions => desc
                ? query.OrderByDescending(x => x.PeriodRedemptions).ThenByDescending(x => x.Promotion.Id)
                : query.OrderBy(x => x.PeriodRedemptions).ThenBy(x => x.Promotion.Id),
            PromotionWire.SortBy.Revenue => desc
                ? query.OrderByDescending(x => x.PeriodRevenueNis).ThenByDescending(x => x.Promotion.Id)
                : query.OrderBy(x => x.PeriodRevenueNis).ThenBy(x => x.Promotion.Id),
            PromotionWire.SortBy.Discount => desc
                ? query.OrderByDescending(x => x.PeriodDiscountNis).ThenByDescending(x => x.Promotion.Id)
                : query.OrderBy(x => x.PeriodDiscountNis).ThenBy(x => x.Promotion.Id),
            // Default: most-recently updated first (created_time fallback).
            _ => query.OrderByDescending(x => x.Promotion.UpdatedDate ?? x.Promotion.CreationTime)
                      .ThenByDescending(x => x.Promotion.Id),
        };
    }

    private static List<string> BuildPayloadIdPatterns(IReadOnlyList<int>? ids, string singularKey, string pluralKey)
    {
        if (ids == null || ids.Count == 0) return new List<string>();
        var patterns = new List<string>(ids.Count * 4);
        foreach (var id in ids)
        {
            var s = id.ToString();
            patterns.Add($"\"{singularKey}\":{s}");
            patterns.Add($"\"{singularKey}\": {s}");
            patterns.Add($"\"{pluralKey}\":[{s}");
            patterns.Add($"\"{pluralKey}\": [{s}");
        }
        return patterns;
    }
}

public sealed class PromotionTabCounts
{
    public int All { get; set; }
    public int Active { get; set; }
    public int Scheduled { get; set; }
    public int Drafts { get; set; }
    public int Ended { get; set; }
    public int EndingWithinWeek { get; set; }
}

public sealed class PromotionPeriodTotals
{
    public int Redemptions { get; set; }
    public decimal RevenueNis { get; set; }
    public decimal DiscountNis { get; set; }
}
