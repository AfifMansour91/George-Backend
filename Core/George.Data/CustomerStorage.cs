using System.Linq;
using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>CRM: Customer is per site (AccountId + SiteId). One row per (SiteId, NormalizedPhone). Delete = soft-delete that row (removes from that site only).</summary>
public class CustomerStorage : StorageBase
{
    public CustomerStorage(GeorgeDBContext dbContext, ILogger<CustomerStorage> logger)
        : base(dbContext, logger)
    {
    }

    private static string DigitsOnly(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    /// <summary>Canonical phone key for dedup/lookup: digits only, +972 -> 0, restore the leading 0
    /// that Excel/some imports strip from 8/9-digit numbers. Empty string when no digits. This is the
    /// single source of truth for Customer.NormalizedPhone - local (05x...) and international
    /// (+972 5x...) formats for the same real number must resolve to the same customer record.</summary>
    internal static string NormalizePhone(string? phone)
    {
        var d = DigitsOnly(phone);
        if (d.Length == 0) return d;
        if ((d.Length == 11 || d.Length == 12) && d.StartsWith("972")) return "0" + d.Substring(3);
        if ((d.Length == 8 || d.Length == 9) && !d.StartsWith("0")) return "0" + d;
        return d;
    }

    private IQueryable<Customer> CustomerSet => _dbContext.Set<Customer>().Where(c => !c.IsDeleted);

    public async Task<Customer?> GetCustomerByPhoneAsync(int siteId, string? phone, CancellationToken cancelToken = default)
    {
        var normalized = NormalizePhone(phone);
        if (normalized.Length < 4) return null;
        return await CustomerSet.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SiteId == siteId && c.NormalizedPhone == normalized, cancelToken);
    }

    /// <summary>Find customer for this site by normalized phone (including soft-deleted rows-reactivates if needed), or create if not found. Used when creating an order so every order has a linked customer. When marketingSms is provided, it is set on create or update so the customer record reflects consent. Structured delivery fields are persisted when non-null (same semantics as city/defaultAddress).</summary>
    public async Task<Customer> GetOrCreateCustomerByPhoneAsync(
        int siteId,
        int accountId,
        string? phone,
        string name,
        string? email,
        string? city,
        string? defaultAddress,
        string? notes,
        bool? marketingSms = null,
        string? deliveryStreet = null,
        string? deliveryApartment = null,
        string? deliveryFloor = null,
        string? deliveryEntranceCode = null,
        Action<Customer>? onCreated = null,
        CancellationToken cancelToken = default)
    {
        var normalized = NormalizePhone(phone);

        // Look up row for this site + phone including soft-deleted (unique index is on SiteId+NormalizedPhone).
        // If the customer was deleted, reactivate instead of inserting a duplicate.
        if (normalized.Length >= 4)
        {
            var existing = await FindCustomerBySiteAndPhoneIncludingDeletedAsync(siteId, normalized, cancelToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                await ApplyCustomerFieldUpdatesAsync(
                    existing,
                    name,
                    email,
                    city,
                    defaultAddress,
                    notes,
                    marketingSms,
                    deliveryStreet,
                    deliveryApartment,
                    deliveryFloor,
                    deliveryEntranceCode,
                    reactivateIfDeleted: true,
                    cancelToken).ConfigureAwait(false);
                return existing;
            }
        }

        // No existing customer for this site + phone: create one
        var newCustomer = new Customer
        {
            AccountId = accountId,
            SiteId = siteId,
            NormalizedPhone = normalized,
            Name = name ?? "",
            Email = email,
            Phone = phone,
            City = city,
            DeliveryStreet = deliveryStreet,
            DeliveryApartment = deliveryApartment,
            DeliveryFloor = deliveryFloor,
            DeliveryEntranceCode = deliveryEntranceCode,
            DefaultAddress = defaultAddress,
            Notes = notes,
            MarketingApproval = false,
            MarketingEmail = false,
            MarketingSms = marketingSms ?? false,
            IsDeleted = false,
            CreationTime = DateTime.UtcNow
        };
        _dbContext.Set<Customer>().Add(newCustomer);
        try
        {
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            onCreated?.Invoke(newCustomer);
            return newCustomer;
        }
        catch (DbUpdateException) when (normalized.Length >= 4)
        {
            _dbContext.Entry(newCustomer).State = EntityState.Detached;
            var existing = await FindCustomerBySiteAndPhoneIncludingDeletedAsync(siteId, normalized, cancelToken)
                .ConfigureAwait(false);
            if (existing == null) throw;
            await ApplyCustomerFieldUpdatesAsync(
                existing,
                name,
                email,
                city,
                defaultAddress,
                notes,
                marketingSms,
                deliveryStreet,
                deliveryApartment,
                deliveryFloor,
                deliveryEntranceCode,
                reactivateIfDeleted: true,
                cancelToken).ConfigureAwait(false);
            return existing;
        }
    }

    private Task<Customer?> FindCustomerBySiteAndPhoneIncludingDeletedAsync(
        int siteId,
        string normalized,
        CancellationToken cancelToken) =>
        _dbContext.Set<Customer>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.SiteId == siteId && c.NormalizedPhone == normalized, cancelToken);

    private async Task ApplyCustomerFieldUpdatesAsync(
        Customer existing,
        string name,
        string? email,
        string? city,
        string? defaultAddress,
        string? notes,
        bool? marketingSms,
        string? deliveryStreet,
        string? deliveryApartment,
        string? deliveryFloor,
        string? deliveryEntranceCode,
        bool reactivateIfDeleted,
        CancellationToken cancelToken)
    {
        var updated = false;
        if (reactivateIfDeleted && existing.IsDeleted)
        {
            existing.IsDeleted = false;
            updated = true;
        }
        if (!string.IsNullOrWhiteSpace(name) && existing.Name != name) { existing.Name = name; updated = true; }
        if (email != null && existing.Email != email) { existing.Email = email; updated = true; }
        if (city != null && existing.City != city) { existing.City = city; updated = true; }
        if (defaultAddress != null && existing.DefaultAddress != defaultAddress) { existing.DefaultAddress = defaultAddress; updated = true; }
        if (deliveryStreet != null && existing.DeliveryStreet != deliveryStreet) { existing.DeliveryStreet = deliveryStreet; updated = true; }
        if (deliveryApartment != null && existing.DeliveryApartment != deliveryApartment) { existing.DeliveryApartment = deliveryApartment; updated = true; }
        if (deliveryFloor != null && existing.DeliveryFloor != deliveryFloor) { existing.DeliveryFloor = deliveryFloor; updated = true; }
        if (deliveryEntranceCode != null && existing.DeliveryEntranceCode != deliveryEntranceCode) { existing.DeliveryEntranceCode = deliveryEntranceCode; updated = true; }
        if (notes != null && existing.Notes != notes) { existing.Notes = notes; updated = true; }
        if (marketingSms.HasValue && existing.MarketingSms != marketingSms.Value) { existing.MarketingSms = marketingSms.Value; updated = true; }
        if (updated)
        {
            existing.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }
    }

    /// <summary>Aggregated customer row for list: customer + site-scoped order stats (orders at the given site only).</summary>
    public class CustomerListRow
    {
        public Customer Customer { get; set; } = null!;
        public int OrderCountAtSite { get; set; }
        public decimal TotalRevenueAtSite { get; set; }
        public int? LastOrderIdAtSite { get; set; }
        public DateTime? LastOrderAtSite { get; set; }
        public int? ChurnThresholdDays { get; set; }
        public bool IsActive { get; set; }
        public bool IsAtRisk { get; set; }
    }

    internal const int DefaultInactiveAfterDays = 14;

    /// <summary>Personal inactivity threshold (days) for a customer: single-order customers use the site default;
    /// customers with history use the average gap between consecutive orders × 2.</summary>
    internal static int ChurnThresholdForOrders(IReadOnlyList<DateTime> orderedDatesAsc, int defaultDays)
    {
        if (orderedDatesAsc.Count <= 1) return defaultDays;
        double sumGaps = 0;
        for (var i = 1; i < orderedDatesAsc.Count; i++)
            sumGaps += (orderedDatesAsc[i] - orderedDatesAsc[i - 1]).TotalDays;
        var avgGap = sumGaps / (orderedDatesAsc.Count - 1);
        var threshold = (int)Math.Round(avgGap * 2);
        return threshold < 1 ? 1 : threshold;
    }

    private async Task<int> GetSiteInactiveAfterDaysAsync(int siteId, CancellationToken cancelToken)
    {
        var configured = await _dbContext.Set<Site>()
            .AsNoTracking()
            .Where(s => s.Id == siteId)
            .Select(s => s.CustomerInactiveAfterDays)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
        return configured is int d && d > 0 ? d : DefaultInactiveAfterDays;
    }

    /// <summary>Per-site setting: whether customer statistics include not-yet-completed orders (default true).</summary>
    private async Task<bool> GetSiteIncludeIncompleteOrdersAsync(int siteId, CancellationToken cancelToken)
    {
        var v = await _dbContext.Set<Site>()
            .AsNoTracking()
            .Where(s => s.Id == siteId)
            .Select(s => s.IncludeIncompleteOrdersInStats)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
        return v ?? true;
    }

    public async Task<DataListResult<CustomerListRow>> GetCustomersAsync(
        CustomerFilter filter,
        PagingExDto paging,
        CancellationToken cancelToken)
    {
        var res = new DataListResult<CustomerListRow>();
        if (filter?.SiteId is not int siteId || siteId <= 0)
        {
            res.Items = new List<CustomerListRow>();
            return res;
        }

        var customersAtSite = await CustomerSet
            .AsNoTracking()
            .Where(c => c.SiteId == siteId)
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var customerIds = customersAtSite.Select(c => c.Id).ToList();
        if (customerIds.Count == 0)
        {
            res.Items = new List<CustomerListRow>();
            return res;
        }

        var includeIncomplete = await GetSiteIncludeIncompleteOrdersAsync(siteId, cancelToken).ConfigureAwait(false);
        var ordersQuery = _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.SiteId == siteId && o.CustomerId != null && customerIds.Contains(o.CustomerId.Value));
        ordersQuery = includeIncomplete
            ? ordersQuery.Where(o => o.Status != "Cancelled")
            : ordersQuery.Where(o => o.Status == "Completed");
        var ordersAtSite = await ordersQuery
            .Select(o => new { o.CustomerId!.Value, o.Total, o.CreationTime, o.Id })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var defaultDays = await GetSiteInactiveAfterDaysAsync(siteId, cancelToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var statsByCustomer = ordersAtSite
            .GroupBy(x => x.Value)
            .ToDictionary(g => g.Key, g =>
            {
                var asc = g.OrderBy(x => x.CreationTime).ToList();
                var last = asc[asc.Count - 1];
                var dates = asc.Select(x => x.CreationTime).ToList();
                var threshold = ChurnThresholdForOrders(dates, defaultDays);
                var atRisk = (now - last.CreationTime).TotalDays >= threshold;
                return (OrderCountAtSite: asc.Count,
                    TotalRevenueAtSite: asc.Where(x => x.Total.HasValue).Sum(x => x.Total!.Value),
                    LastOrderIdAtSite: (int?)last.Id,
                    LastOrderAtSite: (DateTime?)last.CreationTime,
                    ChurnThresholdDays: (int?)threshold,
                    IsAtRisk: atRisk,
                    IsActive: !atRisk);
            });

        var rows = customersAtSite.Select(c =>
        {
            var has = statsByCustomer.TryGetValue(c.Id, out var s);
            return new CustomerListRow
            {
                Customer = c,
                OrderCountAtSite = has ? s.OrderCountAtSite : 0,
                TotalRevenueAtSite = has ? s.TotalRevenueAtSite : 0,
                LastOrderIdAtSite = has ? s.LastOrderIdAtSite : null,
                LastOrderAtSite = has ? s.LastOrderAtSite : null,
                ChurnThresholdDays = has ? s.ChurnThresholdDays : null,
                IsActive = has && s.IsActive,
                IsAtRisk = has && s.IsAtRisk,
            };
        }).ToList();

        if (filter?.Search?.Trim() is { } search)
        {
            var term = search.Trim();
            rows = rows.Where(r =>
                (r.Customer.Name != null && r.Customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.Phone != null && r.Customer.Phone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.Email != null && r.Customer.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.City != null && r.Customer.City.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.DefaultAddress != null && r.Customer.DefaultAddress.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.DeliveryStreet != null && r.Customer.DeliveryStreet.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.DeliveryApartment != null && r.Customer.DeliveryApartment.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.DeliveryFloor != null && r.Customer.DeliveryFloor.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Customer.DeliveryEntranceCode != null && r.Customer.DeliveryEntranceCode.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        if (filter?.Phone?.Trim() is { } phoneFilter)
        {
            var digits = NormalizePhone(phoneFilter);
            if (digits.Length > 0)
            {
                rows = rows.Where(r => (r.Customer.NormalizedPhone ?? NormalizePhone(r.Customer.Phone)).Contains(digits, StringComparison.Ordinal)).ToList();
            }
        }

        var sortBy = (filter?.SortBy ?? "").Trim().ToLowerInvariant();
        var sortDesc = string.Equals(filter?.SortOrder ?? "asc", "desc", StringComparison.OrdinalIgnoreCase);
        rows = sortBy switch
        {
            "ordercount" => sortDesc ? rows.OrderByDescending(c => c.OrderCountAtSite).ToList() : rows.OrderBy(c => c.OrderCountAtSite).ToList(),
            "totalrevenue" => sortDesc ? rows.OrderByDescending(c => c.TotalRevenueAtSite).ToList() : rows.OrderBy(c => c.TotalRevenueAtSite).ToList(),
            _ => sortDesc ? rows.OrderByDescending(c => c.Customer.Name ?? "").ToList() : rows.OrderBy(c => c.Customer.Name ?? "").ToList()
        };

        if (paging.IncludeTotal)
            res.Total = rows.Count;

        res.Items = rows.Skip(paging.Skip).Take(paging.Take).ToList();
        return res;
    }

    public async Task<Customer?> GetCustomerByIdAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        var query = CustomerSet.AsNoTracking().Where(c => c.Id == customerId);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(c => c.SiteId == siteId.Value);
        return await query.FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Global order stats for a customer (all sites). Excludes cancelled orders. Returns (orderCount, totalRevenue, lastOrderId, lastOrderAt, averageReturnDays).</summary>
    public async Task<(int OrderCount, decimal TotalRevenue, int? LastOrderId, DateTime? LastOrderAt, int AverageReturnDays)> GetCustomerGlobalStatsAsync(int customerId, CancellationToken cancelToken)
    {
        // Respect the customer's own site "include incomplete orders in stats" setting.
        var custSiteId = await _dbContext.Set<Customer>().AsNoTracking()
            .Where(c => c.Id == customerId).Select(c => (int?)c.SiteId)
            .FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        var includeIncomplete = custSiteId is int sid ? await GetSiteIncludeIncompleteOrdersAsync(sid, cancelToken).ConfigureAwait(false) : true;

        var ordersQuery = _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.CustomerId == customerId);
        ordersQuery = includeIncomplete
            ? ordersQuery.Where(o => o.Status != "Cancelled")
            : ordersQuery.Where(o => o.Status == "Completed");
        var orders = await ordersQuery
            .Select(o => new { o.Id, o.Total, o.CreationTime })
            .OrderBy(o => o.CreationTime)
            .ToListAsync(cancelToken).ConfigureAwait(false);
        if (orders.Count == 0) return (0, 0, null, null, 0);
        var last = orders[orders.Count - 1];
        var totalRevenue = orders.Where(o => o.Total.HasValue).Sum(o => o.Total!.Value);
        var returnDays = new List<int>();
        for (var i = 1; i < orders.Count; i++)
            returnDays.Add((int)(orders[i].CreationTime - orders[i - 1].CreationTime).TotalDays);
        var averageReturnDays = returnDays.Count > 0 ? (int)Math.Round(returnDays.Average()) : 0;
        return (orders.Count, totalRevenue, last.Id, last.CreationTime, averageReturnDays);
    }

    /// <summary>Point-in-time customer snapshot used for both "now" and "30 days ago" (for trends).</summary>
    private readonly struct StatsSnapshot
    {
        public int ActiveCustomers { get; init; }
        public int AtRiskCustomers { get; init; }
        public int CustomersWithOrders { get; init; }
        public decimal AverageOrdersPerCustomer { get; init; }
        public decimal AverageReturnDays { get; init; }
        public int ReturningPercent { get; init; }
    }

    /// <summary>Computes a snapshot of customer metrics as they stood at <paramref name="refTime"/>, using only orders up to that time.
    /// Each customer's churn threshold is derived from their order cadence as of refTime. Averages exclude customers with 0 orders.</summary>
    private static StatsSnapshot ComputeSnapshot(IEnumerable<List<DateTime>> customerOrderDatesAsc, DateTime refTime, int defaultDays)
    {
        int withOrders = 0, active = 0, atRisk = 0, returning = 0, totalOrders = 0;
        var returnGaps = new List<double>();
        foreach (var allDates in customerOrderDatesAsc)
        {
            var dates = allDates.Count > 0 && allDates[allDates.Count - 1] <= refTime
                ? allDates
                : allDates.Where(d => d <= refTime).ToList();
            if (dates.Count == 0) continue;
            withOrders++;
            totalOrders += dates.Count;
            var threshold = ChurnThresholdForOrders(dates, defaultDays);
            if ((refTime - dates[dates.Count - 1]).TotalDays >= threshold) atRisk++;
            else active++;
            if (dates.Count >= 2)
            {
                returning++;
                for (var i = 1; i < dates.Count; i++)
                    returnGaps.Add((dates[i] - dates[i - 1]).TotalDays);
            }
        }
        return new StatsSnapshot
        {
            ActiveCustomers = active,
            AtRiskCustomers = atRisk,
            CustomersWithOrders = withOrders,
            AverageOrdersPerCustomer = withOrders > 0 ? (decimal)totalOrders / withOrders : 0m,
            AverageReturnDays = returnGaps.Count > 0 ? (decimal)returnGaps.Average() : 0m,
            ReturningPercent = withOrders > 0 ? (int)Math.Round(100m * returning / withOrders) : 0,
        };
    }

    /// <summary>Percent change vs a previous value; null when the previous value is non-positive (can't form a ratio).</summary>
    private static decimal? PercentChange(decimal current, decimal previous)
    {
        if (previous <= 0) return null;
        return Math.Round((current - previous) / previous * 100m, 1);
    }

    public async Task<CustomerStatsDto> GetCustomerStatsAsync(int? siteId, CancellationToken cancelToken)
    {
        var result = new CustomerStatsDto();
        if (siteId is not int sid || sid <= 0) return result;

        var defaultDays = await GetSiteInactiveAfterDaysAsync(sid, cancelToken).ConfigureAwait(false);
        result.ChurnThresholdDays = defaultDays;

        result.TotalCustomers = await CustomerSet.CountAsync(c => c.SiteId == sid, cancelToken).ConfigureAwait(false);
        if (result.TotalCustomers == 0) return result;

        var includeIncomplete = await GetSiteIncludeIncompleteOrdersAsync(sid, cancelToken).ConfigureAwait(false);
        var statsOrdersQuery = _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.SiteId == sid && o.CustomerId != null);
        statsOrdersQuery = includeIncomplete
            ? statsOrdersQuery.Where(o => o.Status != "Cancelled")
            : statsOrdersQuery.Where(o => o.Status == "Completed");
        var ordersAtSite = await statsOrdersQuery
            .Select(o => new { o.CustomerId!.Value, o.CreationTime, o.Total })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        if (ordersAtSite.Count == 0)
        {
            result.ChurnThresholdDays = defaultDays;
            return result;
        }

        var now = DateTime.UtcNow;
        var t30 = now.AddDays(-30);
        var t60 = now.AddDays(-60);

        var customerOrderDates = ordersAtSite
            .GroupBy(x => x.Value)
            .Select(g => g.Select(x => x.CreationTime).OrderBy(d => d).ToList())
            .ToList();

        var current = ComputeSnapshot(customerOrderDates, now, defaultDays);
        result.ActiveCustomers = current.ActiveCustomers;
        result.AtRiskCustomers = current.AtRiskCustomers;
        result.AverageOrdersPerCustomer = current.AverageOrdersPerCustomer;
        result.AverageReturnDays = (int)Math.Round(current.AverageReturnDays);
        result.ReturningCustomersPercent = current.ReturningPercent;

        // AOV: last 30 days (sum of order totals ÷ order count).
        var aovWindow = ordersAtSite.Where(o => o.CreationTime > t30 && o.CreationTime <= now && o.Total.HasValue).ToList();
        result.Aov = aovWindow.Count > 0 ? aovWindow.Sum(o => o.Total!.Value) / aovWindow.Count : 0m;

        // Trends vs 30 days ago - only when the site has ≥30 days of order history.
        var earliestOrder = ordersAtSite.Min(o => o.CreationTime);
        result.HasComparison = (now - earliestOrder).TotalDays >= 30;
        if (result.HasComparison)
        {
            var prior = ComputeSnapshot(customerOrderDates, t30, defaultDays);
            result.ActiveCustomersTrendPercent = PercentChange(current.ActiveCustomers, prior.ActiveCustomers);
            result.AtRiskCustomersTrendPercent = PercentChange(current.AtRiskCustomers, prior.AtRiskCustomers);
            result.AverageOrdersPerCustomerTrend = Math.Round(current.AverageOrdersPerCustomer - prior.AverageOrdersPerCustomer, 1);
            result.AverageReturnDaysTrend = Math.Round(current.AverageReturnDays - prior.AverageReturnDays, 1);

            var aovPrevWindow = ordersAtSite.Where(o => o.CreationTime > t60 && o.CreationTime <= t30 && o.Total.HasValue).ToList();
            var aovPrev = aovPrevWindow.Count > 0 ? aovPrevWindow.Sum(o => o.Total!.Value) / aovPrevWindow.Count : 0m;
            result.AovTrendPercent = PercentChange(result.Aov, aovPrev);
        }

        return result;
    }

    /// <summary>Create a new customer for the given site from the customers screen. The owning account is resolved from the site. When a phone is supplied that already exists for the site, the existing (or soft-deleted) row is updated/reactivated instead of inserting a duplicate.</summary>
    public async Task<Customer?> CreateCustomerAsync(
        int siteId,
        string name,
        string? phone,
        string? email,
        string? city,
        string? notes,
        Action<Customer>? onCreated = null,
        CancellationToken cancelToken = default)
    {
        var accountId = await _dbContext.Set<Site>()
            .AsNoTracking()
            .Where(s => s.Id == siteId)
            .Select(s => (int?)s.AccountId)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
        if (accountId == null) return null;

        return await GetOrCreateCustomerByPhoneAsync(
            siteId,
            accountId.Value,
            phone,
            name,
            email,
            city,
            defaultAddress: null,
            notes: notes,
            onCreated: onCreated,
            cancelToken: cancelToken).ConfigureAwait(false);
    }


    public class ImportRow
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? DeliveryStreet { get; set; }
        public string? DeliveryApartment { get; set; }
        public string? DeliveryFloor { get; set; }
        public string? DeliveryEntranceCode { get; set; }
        public string? Notes { get; set; }
        public bool MarketingApproval { get; set; }
    }

    public class ImportRowIssue
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string Status { get; set; } = "";  // "skipped" | "failed"
        public string? Reason { get; set; }
    }

    public class ImportResult
    {
        public int Total { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<ImportRowIssue> Issues { get; set; } = new();
    }

    /// <summary>Bulk import spreadsheet rows. Rows with a phone (≥4 digits after canonicalization) are matched by (SiteId, NormalizedPhone); phone-less rows are matched by email. Existing customers are ENRICHED only - empty fields filled, marketing consent OR-ed in - never overwritten. Soft-deleted matches are skipped (import must not resurrect deleted customers). Returns null when the site does not exist.</summary>
    public async Task<ImportResult?> ImportCustomersAsync(
        int siteId,
        IReadOnlyList<ImportRow> rows,
        Action<Customer>? onCreated = null,
        CancellationToken cancelToken = default)
    {
        var accountId = await _dbContext.Set<Site>()
            .AsNoTracking()
            .Where(s => s.Id == siteId)
            .Select(s => (int?)s.AccountId)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
        if (accountId == null) return null;

        var result = new ImportResult { Total = rows.Count };
        var seenPhones = new HashSet<string>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const int batchSize = 200;
        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            var batch = rows.Skip(offset).Take(batchSize).ToList();

            var batchPhones = batch
                .Select(r => NormalizePhone(r.Phone))
                .Where(p => p.Length >= 4)
                .Distinct()
                .ToList();
            var batchEmails = batch
                .Where(r => NormalizePhone(r.Phone).Length < 4 && !string.IsNullOrWhiteSpace(r.Email))
                .Select(r => r.Email!.Trim().ToLower())
                .Distinct()
                .ToList();

            // Load matches tracked (including soft-deleted) so enrichment saves with the batch.
            var byPhone = (await _dbContext.Set<Customer>()
                    .Where(c => c.SiteId == siteId && batchPhones.Contains(c.NormalizedPhone))
                    .ToListAsync(cancelToken).ConfigureAwait(false))
                .ToDictionary(c => c.NormalizedPhone);
            var byEmail = (await _dbContext.Set<Customer>()
                    .Where(c => c.SiteId == siteId && !c.IsDeleted && c.Email != null && batchEmails.Contains(c.Email.ToLower()))
                    .ToListAsync(cancelToken).ConfigureAwait(false))
                .GroupBy(c => c.Email!.ToLower())
                .ToDictionary(g => g.Key, g => g.First());

            var createdInBatch = new List<Customer>();
            foreach (var row in batch)
            {
                var norm = NormalizePhone(row.Phone);
                var email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim();
                var hasPhone = norm.Length >= 4;

                if (!hasPhone && email == null)
                {
                    result.Skipped++;
                    result.Issues.Add(new ImportRowIssue { Name = row.Name, Phone = row.Phone, Email = row.Email, Status = "skipped", Reason = "אין טלפון ואין אימייל" });
                    continue;
                }
                if (hasPhone ? !seenPhones.Add(norm) : !seenEmails.Add(email!))
                {
                    result.Skipped++;
                    result.Issues.Add(new ImportRowIssue { Name = row.Name, Phone = row.Phone, Email = row.Email, Status = "skipped", Reason = "שורה כפולה בקובץ" });
                    continue;
                }

                Customer? existing = hasPhone
                    ? byPhone.GetValueOrDefault(norm)
                    : byEmail.GetValueOrDefault(email!.ToLower());

                if (existing != null)
                {
                    if (existing.IsDeleted)
                    {
                        result.Skipped++;
                        result.Issues.Add(new ImportRowIssue { Name = row.Name, Phone = row.Phone, Email = row.Email, Status = "skipped", Reason = "לקוח עם הטלפון הזה נמחק מהמערכת" });
                        continue;
                    }
                    EnrichCustomerFromImport(existing, row, email);
                    result.Updated++;
                    continue;
                }

                var street = Capped(row.DeliveryStreet, 400);
                var city = Capped(row.City, 200);
                var created = new Customer
                {
                    AccountId = accountId.Value,
                    SiteId = siteId,
                    NormalizedPhone = hasPhone ? norm : string.Empty,
                    Name = Capped(row.Name, 200) ?? "",
                    Phone = hasPhone ? norm : null,
                    Email = Capped(email, 200),
                    City = city,
                    DeliveryStreet = street,
                    DeliveryApartment = Capped(row.DeliveryApartment, 64),
                    DeliveryFloor = Capped(row.DeliveryFloor, 32),
                    DeliveryEntranceCode = Capped(row.DeliveryEntranceCode, 64),
                    DefaultAddress = Capped(street != null && city != null ? $"{street}, {city}" : street, 500),
                    Notes = Capped(row.Notes, 2000),
                    MarketingApproval = row.MarketingApproval,
                    MarketingEmail = row.MarketingApproval,
                    MarketingSms = row.MarketingApproval,
                    IsDeleted = false,
                    CreationTime = DateTime.UtcNow
                };
                _dbContext.Set<Customer>().Add(created);
                createdInBatch.Add(created);
                // Track the row so a same-phone/email row later in the file counts as duplicate,
                // and add to the match maps so later rows enrich instead of double-inserting.
                if (hasPhone) byPhone[norm] = created;
                else byEmail[email!.ToLower()] = created;
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                result.Created += createdInBatch.Count;
                if (onCreated != null)
                    foreach (var c in createdInBatch) onCreated(c);
            }
            catch (DbUpdateException)
            {
                // Batch failed - isolate. First persist the enrichments alone (detach all creates);
                // if even that fails, drop the enriched changes too so they can't poison the per-row saves.
                foreach (var c in createdInBatch)
                    _dbContext.Entry(c).State = EntityState.Detached;
                try
                {
                    await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                }
                catch (DbUpdateException ex)
                {
                    foreach (var entry in _dbContext.ChangeTracker.Entries<Customer>().Where(e => e.State == EntityState.Modified).ToList())
                    {
                        result.Updated--;
                        result.Failed++;
                        result.Issues.Add(new ImportRowIssue { Name = entry.Entity.Name, Phone = entry.Entity.Phone, Email = entry.Entity.Email, Status = "failed", Reason = ex.InnerException?.Message ?? ex.Message });
                        entry.State = EntityState.Detached;
                    }
                }
                foreach (var c in createdInBatch)
                {
                    _dbContext.Entry(c).State = EntityState.Added;
                    try
                    {
                        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                        result.Created++;
                        onCreated?.Invoke(c);
                    }
                    catch (DbUpdateException ex)
                    {
                        _dbContext.Entry(c).State = EntityState.Detached;
                        result.Failed++;
                        result.Issues.Add(new ImportRowIssue { Name = c.Name, Phone = c.Phone, Email = c.Email, Status = "failed", Reason = ex.InnerException?.Message ?? ex.Message });
                    }
                }
            }
        }

        return result;
    }

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Trim + cap to the column's max length - spreadsheet cells often carry free text longer than the schema allows (e.g. a note typed into the "floor" column), which would fail the whole batch with a truncation error.</summary>
    private static string? Capped(string? s, int max)
    {
        var t = Trimmed(s);
        if (t == null) return null;
        return t.Length <= max ? t : t.Substring(0, max);
    }

    /// <summary>Fill-empty enrichment for import: never overwrites data the shop already has; marketing consent only turns ON. Incoming values are capped to column lengths.</summary>
    private static void EnrichCustomerFromImport(Customer existing, ImportRow row, string? email)
    {
        var updated = false;
        void Fill(string? current, string? incoming, int max, Action<string> set)
        {
            var capped = Capped(incoming, max);
            if (string.IsNullOrWhiteSpace(current) && capped != null) { set(capped); updated = true; }
        }
        Fill(existing.Name, row.Name, 200, v => existing.Name = v);
        Fill(existing.Email, email, 200, v => existing.Email = v);
        Fill(existing.City, row.City, 200, v => existing.City = v);
        Fill(existing.DeliveryStreet, row.DeliveryStreet, 400, v => existing.DeliveryStreet = v);
        Fill(existing.DeliveryApartment, row.DeliveryApartment, 64, v => existing.DeliveryApartment = v);
        Fill(existing.DeliveryFloor, row.DeliveryFloor, 32, v => existing.DeliveryFloor = v);
        Fill(existing.DeliveryEntranceCode, row.DeliveryEntranceCode, 64, v => existing.DeliveryEntranceCode = v);
        var street = Trimmed(row.DeliveryStreet);
        var city = Trimmed(row.City);
        Fill(existing.DefaultAddress, street != null && city != null ? $"{street}, {city}" : street, 500, v => existing.DefaultAddress = v);
        var notes = Trimmed(row.Notes);
        if (notes != null && (existing.Notes == null || !existing.Notes.Contains(notes)))
        {
            existing.Notes = Capped(string.IsNullOrWhiteSpace(existing.Notes) ? notes : $"{existing.Notes} | {notes}", 2000);
            updated = true;
        }
        if (row.MarketingApproval)
        {
            if (!existing.MarketingApproval) { existing.MarketingApproval = true; updated = true; }
            if (!existing.MarketingEmail) { existing.MarketingEmail = true; updated = true; }
            if (!existing.MarketingSms) { existing.MarketingSms = true; updated = true; }
        }
        if (updated) existing.UpdatedDate = DateTime.UtcNow;
    }

    public async Task<Order?> GetLastOrderByCustomerIdAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        // Scope to the customer's OWN branch (a Customer row is per-site, so this is the branch the customer
        // belongs to). We intentionally do NOT use the caller's `siteId` (the globally-selected site filter) -
        // that caused the "last order empty while count > 0" bug when a different branch was selected.
        var customerSiteId = await _dbContext.Set<Customer>()
            .Where(c => c.Id == customerId)
            .Select(c => (int?)c.SiteId)
            .FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);

        var query = _dbContext.Order
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.CustomerId == customerId);
        if (customerSiteId.HasValue)
            query = query.Where(o => o.SiteId == customerSiteId.Value);
        var lastOrderId = await query.OrderByDescending(o => o.CreationTime).Select(o => o.Id).FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (lastOrderId == 0) return null;
        return await _dbContext.Order
            .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == lastOrderId && !o.IsDeleted, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Set or clear the phone-order permanent discount stored on the customer record.</summary>
    public async Task SetCustomerPermanentDiscountAsync(
        int customerId,
        string? discountType,
        decimal? discountValue,
        bool clear,
        CancellationToken cancelToken = default)
    {
        var c = await _dbContext.Set<Customer>()
            .FirstOrDefaultAsync(x => x.Id == customerId && !x.IsDeleted, cancelToken)
            .ConfigureAwait(false);
        if (c == null) return;

        if (clear)
        {
            c.PermanentDiscountType = null;
            c.PermanentDiscountValue = null;
        }
        else
        {
            var type = (discountType ?? "percent").Trim().ToLowerInvariant();
            if (type != "percent" && type != "amount") type = "percent";
            c.PermanentDiscountType = type;
            c.PermanentDiscountValue = discountValue;
        }

        c.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Update customer CRM fields (name, note, contact, address, marketing). Null fields are left unchanged.
    /// Returns (customer, phoneConflict): when the new phone collides with another customer at the site, returns (null, true).</summary>
    public async Task<(Customer? Customer, bool PhoneConflict)> UpdateCustomerAsync(
        int customerId, int? siteId,
        string name, string? notes, string? email, string? phone, string? city,
        string? deliveryStreet, string? deliveryApartment, string? deliveryFloor, string? deliveryEntranceCode,
        bool? marketingEmail, bool? marketingSms,
        CancellationToken cancelToken)
    {
        var query = _dbContext.Set<Customer>().Where(x => x.Id == customerId && !x.IsDeleted);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(x => x.SiteId == siteId.Value);
        var c = await query.FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (c == null) return (null, false);

        c.Name = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
        if (notes != null) c.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (email != null) c.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (city != null) c.City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (deliveryStreet != null) c.DeliveryStreet = string.IsNullOrWhiteSpace(deliveryStreet) ? null : deliveryStreet.Trim();
        if (deliveryApartment != null) c.DeliveryApartment = string.IsNullOrWhiteSpace(deliveryApartment) ? null : deliveryApartment.Trim();
        if (deliveryFloor != null) c.DeliveryFloor = string.IsNullOrWhiteSpace(deliveryFloor) ? null : deliveryFloor.Trim();
        if (deliveryEntranceCode != null) c.DeliveryEntranceCode = string.IsNullOrWhiteSpace(deliveryEntranceCode) ? null : deliveryEntranceCode.Trim();
        if (marketingEmail.HasValue) c.MarketingEmail = marketingEmail.Value;
        if (marketingSms.HasValue) c.MarketingSms = marketingSms.Value;

        if (phone != null)
        {
            var newNormalized = NormalizePhone(phone);
            if (newNormalized != c.NormalizedPhone)
            {
                if (newNormalized.Length >= 4)
                {
                    var conflict = await _dbContext.Set<Customer>()
                        .IgnoreQueryFilters()
                        .AnyAsync(x => x.SiteId == c.SiteId && x.Id != c.Id && x.NormalizedPhone == newNormalized, cancelToken)
                        .ConfigureAwait(false);
                    if (conflict) return (null, true);
                }
                c.NormalizedPhone = newNormalized;
            }
            c.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        }

        c.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return (c, false);
    }

    public async Task<Dictionary<int, string?>> GetNotesByCustomerIdsAsync(
        IEnumerable<int> customerIds,
        CancellationToken cancelToken = default)
    {
        var ids = customerIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return new Dictionary<int, string?>();

        return await CustomerSet.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Notes })
            .ToDictionaryAsync(x => x.Id, x => x.Notes, cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>Non-cancelled, non-deleted order ids for a customer (site-scoped when siteId provided). Used when deleting a customer "with orders".</summary>
    public async Task<List<int>> GetActiveOrderIdsByCustomerAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        var query = _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.CustomerId == customerId);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(o => o.SiteId == siteId.Value);
        return await query.Select(o => o.Id).ToListAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Soft-delete customer (removes from that site only). If siteId is provided, only deletes when customer belongs to that site.</summary>
    public async Task<bool> DeleteCustomerAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        var query = _dbContext.Set<Customer>().Where(x => x.Id == customerId && !x.IsDeleted);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(x => x.SiteId == siteId.Value);
        var c = await query.FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (c == null) return false;
        c.IsDeleted = true;
        c.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Soft-delete customers (from site only). If siteId is provided, only deletes rows that belong to that site.</summary>
    public async Task DeleteCustomersAsync(IEnumerable<int> ids, int? siteId, CancellationToken cancelToken)
    {
        var idList = ids?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        if (idList.Count == 0) return;
        var query = _dbContext.Set<Customer>().Where(x => idList.Contains(x.Id) && !x.IsDeleted);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(x => x.SiteId == siteId.Value);
        var list = await query.ToListAsync(cancelToken).ConfigureAwait(false);
        foreach (var c in list)
        {
            c.IsDeleted = true;
            c.UpdatedDate = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    public class CustomerStatsDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public decimal? ActiveCustomersTrendPercent { get; set; }
        public decimal Aov { get; set; }
        public decimal? AovTrendPercent { get; set; }
        public decimal AverageOrdersPerCustomer { get; set; }
        public decimal? AverageOrdersPerCustomerTrend { get; set; }
        public int AverageReturnDays { get; set; }
        public decimal? AverageReturnDaysTrend { get; set; }
        public int AtRiskCustomers { get; set; }
        public decimal? AtRiskCustomersTrendPercent { get; set; }
        public int ChurnThresholdDays { get; set; }
        public bool HasComparison { get; set; }
        public int ReturningCustomersPercent { get; set; }
    }
}
