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

        var metricAgg = BuildMetricAggregationQuery(fromDay, toDay);

        var basePromotions = ApplyPromotionFilters(_dbContext.Promotion.AsNoTracking(), filter, utcNow);

        var joined =
            from p in basePromotions
            join g in metricAgg on p.Id equals g.PromotionId into gj
            from g in gj.DefaultIfEmpty()
            select new PromotionListRow
            {
                Promotion = p,
                PeriodRedemptions = g == null ? 0 : g.Redemptions,
                PeriodRevenueNis = g == null ? 0m : g.RevenueNis,
                PeriodDiscountNis = g == null ? 0m : g.DiscountNis,
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

        var promoQuery = ApplyKpiPromotionFilters(
            _dbContext.Promotion.AsNoTracking().Where(p => p.SiteId == siteId),
            channel,
            discountKind);

        var ids = await promoQuery.Select(p => p.Id).ToListAsync(cancelToken).ConfigureAwait(false);
        if (ids.Count == 0)
            return new PromotionPeriodTotals();

        var mq = _dbContext.PromotionDailyMetric.AsNoTracking().Where(m => ids.Contains(m.PromotionId));
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

    private IQueryable<MetricAgg> BuildMetricAggregationQuery(DateTime? fromDay, DateTime? toDay)
    {
        var mq = _dbContext.PromotionDailyMetric.AsNoTracking();
        if (fromDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc >= fromDay.Value);
        if (toDay.HasValue)
            mq = mq.Where(m => m.MetricDateUtc <= toDay.Value);

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

    private static IQueryable<Promotion> ApplyPromotionFilters(IQueryable<Promotion> query, PromotionFilter filter, DateTime utcNowDate)
    {
        if (!string.IsNullOrWhiteSpace(filter.PromotionType))
        {
            var t = filter.PromotionType.Trim();
            query = query.Where(p => p.PromotionType == t);
        }

        query = ApplyListTab(query, filter.ListTab, utcNowDate);

        if (filter.Search?.SearchTerm.HasValue() == true)
        {
            var term = filter.Search!.SearchTerm!.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.PayloadJson.Contains(term)
                || (p.CouponCode != null && p.CouponCode.Contains(term))
                || (p.AppliesToSummary != null && p.AppliesToSummary.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.DiscountKind)
            && !filter.DiscountKind.Equals(PromotionWire.DiscountKind.All, StringComparison.OrdinalIgnoreCase))
        {
            var dk = filter.DiscountKind.Trim().ToLowerInvariant();
            query = query.Where(p => p.ListDiscountKind.ToLower() == dk);
        }

        if (!string.IsNullOrWhiteSpace(filter.Channel)
            && !filter.Channel.Equals(PromotionWire.Channel.All, StringComparison.OrdinalIgnoreCase))
        {
            var token = "\"" + filter.Channel.Trim().ToLowerInvariant() + "\"";
            query = query.Where(p => (p.ChannelsJson ?? PromotionWire.DefaultChannelsJson)!.Contains(token));
        }

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
