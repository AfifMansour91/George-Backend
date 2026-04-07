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

    internal static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private IQueryable<Customer> CustomerSet => _dbContext.Set<Customer>().Where(c => !c.IsDeleted);

    /// <summary>Find customer for this site by normalized phone, or create if not found. Used when creating an order so every order has a linked customer. When marketingSms is provided, it is set on create or update so the customer record reflects consent. Structured delivery fields are persisted when non-null (same semantics as city/defaultAddress).</summary>
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
        CancellationToken cancelToken = default)
    {
        var normalized = NormalizePhone(phone);

        // Look up existing customer for this site + phone (unless phone too short to match)
        if (normalized.Length >= 4)
        {
            var existing = await _dbContext.Set<Customer>()
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.SiteId == siteId && c.NormalizedPhone == normalized, cancelToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                var updated = false;
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
                return existing;
            }
        }

        // No existing customer for this site + phone: create one
        {
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
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return newCustomer;
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

        var ordersAtSite = await _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.SiteId == siteId && o.CustomerId != null && customerIds.Contains(o.CustomerId.Value))
            .Select(o => new { o.CustomerId!.Value, o.Total, o.CreationTime, o.Id })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var statsByCustomer = ordersAtSite
            .GroupBy(x => x.Value)
            .ToDictionary(g => g.Key, g =>
            {
                var list = g.OrderByDescending(x => x.CreationTime).ToList();
                var last = list[0];
                return (OrderCountAtSite: list.Count,
                    TotalRevenueAtSite: list.Where(x => x.Total.HasValue).Sum(x => x.Total!.Value),
                    LastOrderIdAtSite: (int?)last.Id,
                    LastOrderAtSite: (DateTime?)last.CreationTime);
            });

        var rows = customersAtSite.Select(c => new CustomerListRow
        {
            Customer = c,
            OrderCountAtSite = statsByCustomer.TryGetValue(c.Id, out var s) ? s.OrderCountAtSite : 0,
            TotalRevenueAtSite = statsByCustomer.TryGetValue(c.Id, out var s2) ? s2.TotalRevenueAtSite : 0,
            LastOrderIdAtSite = statsByCustomer.TryGetValue(c.Id, out var s3) ? s3.LastOrderIdAtSite : null,
            LastOrderAtSite = statsByCustomer.TryGetValue(c.Id, out var s4) ? s4.LastOrderAtSite : null
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
        var orders = await _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.CustomerId == customerId)
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

    public async Task<CustomerStatsDto> GetCustomerStatsAsync(int? siteId, CancellationToken cancelToken)
    {
        var result = new CustomerStatsDto();
        if (siteId is not int sid || sid <= 0) return result;

        result.TotalCustomers = await CustomerSet.CountAsync(c => c.SiteId == sid, cancelToken).ConfigureAwait(false);
        if (result.TotalCustomers == 0) return result;

        var ordersAtSite = await _dbContext.Order
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.SiteId == sid && o.CustomerId != null)
            .Select(o => new { o.CustomerId!.Value, o.CreationTime })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var byCustomer = ordersAtSite.GroupBy(x => x.Value).Select(g => g.OrderBy(x => x.CreationTime).ToList()).ToList();
        var totalOrders = byCustomer.Sum(x => x.Count);
        result.AverageOrdersPerCustomer = (decimal)totalOrders / result.TotalCustomers;
        result.ReturningCustomersPercent = (int)Math.Round(100m * byCustomer.Count(x => x.Count > 1) / result.TotalCustomers);

        var returnDays = new List<int>();
        foreach (var list in byCustomer.Where(x => x.Count >= 2))
        {
            for (var i = 1; i < list.Count; i++)
                returnDays.Add((int)(list[i].CreationTime - list[i - 1].CreationTime).TotalDays);
        }
        result.AverageReturnDays = returnDays.Count > 0 ? (int)Math.Round(returnDays.Average()) : 0;
        return result;
    }

    public async Task<Order?> GetLastOrderByCustomerIdAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        var query = _dbContext.Order
            .Where(o => !o.IsDeleted && o.Status != "Cancelled" && o.CustomerId == customerId);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(o => o.SiteId == siteId.Value);
        var lastOrderId = await query.OrderByDescending(o => o.CreationTime).Select(o => o.Id).FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (lastOrderId == 0) return null;
        return await _dbContext.Order
            .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == lastOrderId && !o.IsDeleted, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Update customer name. If siteId is provided, only updates when customer belongs to that site. Returns updated customer or null if not found.</summary>
    public async Task<Customer?> UpdateCustomerAsync(int customerId, int? siteId, string name, CancellationToken cancelToken)
    {
        var query = _dbContext.Set<Customer>().Where(x => x.Id == customerId && !x.IsDeleted);
        if (siteId.HasValue && siteId.Value > 0)
            query = query.Where(x => x.SiteId == siteId.Value);
        var c = await query.FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (c == null) return null;
        c.Name = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
        c.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return c;
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
        public int ReturningCustomersPercent { get; set; }
        public int AverageReturnDays { get; set; }
        public decimal AverageOrdersPerCustomer { get; set; }
    }
}
