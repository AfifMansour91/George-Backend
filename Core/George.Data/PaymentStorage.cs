using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class PaymentStorage : StorageBase
{
    public PaymentStorage(GeorgeDBContext dbContext, ILogger<PaymentStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<Order?> GetOrderForPaymentAsync(int orderId, CancellationToken cancelToken) =>
        await _dbContext.Order
            .Include(o => o.Site)
            .Include(o => o.CustomerPaymentMethod)
            .Include(o => o.OrderItem.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);

    public async Task<Site?> GetSitePaymentConfigAsync(int siteId, CancellationToken cancelToken) =>
        await _dbContext.Site.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == siteId && !s.IsDeleted, cancelToken);

    public async Task UpdateSitePaymentConfigAsync(Site site, CancellationToken cancelToken)
    {
        var tracked = await _dbContext.Site.FirstOrDefaultAsync(s => s.Id == site.Id, cancelToken);
        if (tracked == null) return;
        tracked.PaymentGatewayProvider = site.PaymentGatewayProvider;
        tracked.CardcomTerminalNumber = site.CardcomTerminalNumber;
        tracked.CardcomApiName = site.CardcomApiName;
        tracked.CardcomApiPasswordEncrypted = site.CardcomApiPasswordEncrypted;
        tracked.CardcomSaveCardEnabled = site.CardcomSaveCardEnabled;
        tracked.PaymentAuthBufferPercent = site.PaymentAuthBufferPercent;
        tracked.PaymentMaxAuthAmount = site.PaymentMaxAuthAmount;
        tracked.PaymentAllowCaptureAboveAuth = site.PaymentAllowCaptureAboveAuth;
        tracked.CardcomProviderExtrasJson = site.CardcomProviderExtrasJson;
        tracked.CardcomCssUrl = site.CardcomCssUrl;
        tracked.CardcomLogoUrl = site.CardcomLogoUrl;
        tracked.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken);
    }

    public async Task AddPaymentEventAsync(OrderPaymentEvent evt, CancellationToken cancelToken)
    {
        evt.CreationTime = DateTime.UtcNow;
        _dbContext.OrderPaymentEvent.Add(evt);
        await _dbContext.SaveChangesAsync(cancelToken);
    }

    public async Task<List<OrderPaymentEvent>> GetPaymentEventsAsync(int orderId, CancellationToken cancelToken) =>
        await _dbContext.OrderPaymentEvent.AsNoTracking()
            .Where(e => e.OrderId == orderId)
            .OrderByDescending(e => e.CreationTime)
            .ToListAsync(cancelToken);

    public async Task<CustomerPaymentMethod?> GetDefaultPaymentMethodAsync(int customerId, int siteId, CancellationToken cancelToken) =>
        await _dbContext.CustomerPaymentMethod
            .Where(m => m.CustomerId == customerId && m.SiteId == siteId && !m.IsRetired)
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.CreationTime)
            .FirstOrDefaultAsync(cancelToken);

    public async Task<CustomerPaymentMethod?> GetPaymentMethodByIdAsync(int id, CancellationToken cancelToken) =>
        await _dbContext.CustomerPaymentMethod
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsRetired, cancelToken);

    public async Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancelToken) =>
        await _dbContext.Set<Customer>().AnyAsync(c => c.Id == customerId && !c.IsDeleted, cancelToken);

    public async Task UpdatePaymentMethodDisplayFieldsAsync(
        int paymentMethodId,
        string? last4Digits,
        string? cardBrand,
        string? tokenExDate,
        CancellationToken cancelToken) =>
        await ForceUpdatePaymentMethodDisplayFieldsAsync(
            paymentMethodId, last4Digits, cardBrand, tokenExDate, onlyIfEmpty: true, cancelToken);

    public async Task ForceUpdatePaymentMethodDisplayFieldsAsync(
        int paymentMethodId,
        string? last4Digits,
        string? cardBrand,
        string? tokenExDate,
        bool onlyIfEmpty,
        CancellationToken cancelToken)
    {
        var last4 = NormalizeLast4Digits(last4Digits);
        var brand = string.IsNullOrWhiteSpace(cardBrand) ? null : cardBrand.Trim();
        var tokenEx = string.IsNullOrWhiteSpace(tokenExDate) ? null : tokenExDate.Trim();
        if (last4 == null && brand == null && tokenEx == null)
            return;

        if (onlyIfEmpty)
        {
            var pm = await _dbContext.CustomerPaymentMethod
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == paymentMethodId && !m.IsRetired, cancelToken);
            if (pm == null)
                return;
            if (!string.IsNullOrWhiteSpace(last4) && !string.IsNullOrWhiteSpace(pm.Last4Digits))
                last4 = null;
            if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(pm.CardBrand))
                brand = null;
            if (!string.IsNullOrWhiteSpace(tokenEx) && !string.IsNullOrWhiteSpace(pm.TokenExDate))
                tokenEx = null;
            if (last4 == null && brand == null && tokenEx == null)
                return;
        }

        var query = _dbContext.CustomerPaymentMethod.Where(m => m.Id == paymentMethodId && !m.IsRetired);
        if (last4 != null)
            await query.ExecuteUpdateAsync(s => s.SetProperty(m => m.Last4Digits, last4), cancelToken);
        if (brand != null)
            await query.ExecuteUpdateAsync(s => s.SetProperty(m => m.CardBrand, brand), cancelToken);
        if (tokenEx != null)
            await query.ExecuteUpdateAsync(s => s.SetProperty(m => m.TokenExDate, tokenEx), cancelToken);
    }

    public async Task<List<int>> GetPaymentMethodIdsMissingDisplayAsync(int? siteId, CancellationToken cancelToken)
    {
        var query = _dbContext.CustomerPaymentMethod.AsNoTracking().Where(m => !m.IsRetired);
        if (siteId is int sid)
            query = query.Where(m => m.SiteId == sid);

        return await query
            .Where(m => m.Last4Digits == null || m.CardBrand == null)
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.CreationTime)
            .Select(m => m.Id)
            .ToListAsync(cancelToken);
    }

    public async Task<int?> GetLatestOrderIdForPaymentMethodAsync(int paymentMethodId, CancellationToken cancelToken)
    {
        var byPaymentMethod = await _dbContext.Order.AsNoTracking()
            .Where(o => !o.IsDeleted && o.CustomerPaymentMethodId == paymentMethodId)
            .OrderByDescending(o => o.CreationTime)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync(cancelToken);
        if (byPaymentMethod is > 0)
            return byPaymentMethod;

        var pm = await _dbContext.CustomerPaymentMethod.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == paymentMethodId && !m.IsRetired, cancelToken);
        if (pm == null)
            return null;

        return await _dbContext.Order.AsNoTracking()
            .Where(o => !o.IsDeleted && o.SiteId == pm.SiteId && o.CustomerId == pm.CustomerId)
            .OrderByDescending(o => o.CreationTime)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync(cancelToken);
    }

    private static string? NormalizeLast4Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;
        return digits.Length <= 4 ? digits.PadLeft(4, '0') : digits[^4..];
    }

    public async Task<CustomerPaymentMethod> SavePaymentMethodAsync(
        CustomerPaymentMethod method,
        CancellationToken cancelToken)
    {
        var existing = await _dbContext.CustomerPaymentMethod
            .Where(m => m.CustomerId == method.CustomerId && m.SiteId == method.SiteId && !m.IsRetired)
            .ToListAsync(cancelToken);

        foreach (var m in existing)
            m.IsDefault = false;

        method.IsDefault = true;
        method.CreationTime = DateTime.UtcNow;
        _dbContext.CustomerPaymentMethod.Add(method);
        await _dbContext.SaveChangesAsync(cancelToken);

        await ForceUpdatePaymentMethodDisplayFieldsAsync(
            method.Id,
            method.Last4Digits,
            method.CardBrand,
            method.TokenExDate,
            onlyIfEmpty: false,
            cancelToken);

        return method;
    }

    public async Task SaveOrderPaymentStateAsync(Order order, CancellationToken cancelToken)
    {
        var tracked = await _dbContext.Order.FirstOrDefaultAsync(o => o.Id == order.Id, cancelToken);
        if (tracked == null) return;

        tracked.PaymentStatus = order.PaymentStatus;
        tracked.PaymentSettleStatus = order.PaymentSettleStatus;
        tracked.PaymentAuthorizedAmount = order.PaymentAuthorizedAmount;
        tracked.CardcomLowProfileId = order.CardcomLowProfileId;
        tracked.CardcomSuspendedDealId = order.CardcomSuspendedDealId;
        tracked.CardcomApprovalNumber = order.CardcomApprovalNumber;
        tracked.CardcomTokenLast4 = order.CardcomTokenLast4;
        tracked.CardcomCardBrand = order.CardcomCardBrand;
        tracked.CustomerPaymentMethodId = order.CustomerPaymentMethodId;
        tracked.CardcomPaymentJson = order.CardcomPaymentJson;
        if (order.CustomerId is int customerId)
            tracked.CustomerId = customerId;
        tracked.PaymentReference = order.PaymentReference;
        tracked.GatewayPaymentTransactionId = order.GatewayPaymentTransactionId;
        tracked.PaymentGateway = order.PaymentGateway;
        tracked.InvoiceNumber = order.InvoiceNumber;
        tracked.CardcomDocumentUrl = order.CardcomDocumentUrl;
        tracked.PaidAt = order.PaidAt;
        tracked.ExternalPaymentStatus = order.ExternalPaymentStatus;
        tracked.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken);
    }

    private static string NormalizePhoneDigits(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? "" : new string(phone.Where(char.IsDigit).ToArray());

    private static bool IsCardcomCreditPaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return false;
        var m = method.Trim();
        return m.Equals("SavedCard", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditCard", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditPhone", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditSms", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolve saved card display for manual order UI: CRM payment method first, then recent orders for same phone.
    /// </summary>
    public async Task<(bool HasCard, string? Last4Digits, string? CardBrand, int? CustomerPaymentMethodId)?>
        ResolveSavedCardByPhoneAsync(int siteId, string? phone, int? customerId, CancellationToken cancelToken)
    {
        if (siteId <= 0) return null;

        if (customerId is int cid && await CustomerExistsAsync(cid, cancelToken))
        {
            var pmById = await GetDefaultPaymentMethodAsync(cid, siteId, cancelToken);
            if (pmById != null)
                return (true, pmById.Last4Digits, pmById.CardBrand, pmById.Id);
        }

        var normalized = NormalizePhoneDigits(phone);
        if (normalized.Length >= 4)
        {
            var customer = await _dbContext.Set<Customer>().AsNoTracking()
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.SiteId == siteId && c.NormalizedPhone == normalized,
                    cancelToken);
            if (customer != null)
            {
                var pm = await GetDefaultPaymentMethodAsync(customer.Id, siteId, cancelToken);
                if (pm != null)
                    return (true, pm.Last4Digits, pm.CardBrand, pm.Id);
            }
        }

        if (normalized.Length < 4) return null;

        var recentOrders = await _dbContext.Order.AsNoTracking()
            .Where(o => !o.IsDeleted && o.SiteId == siteId && o.CustomerPhone != null)
            .OrderByDescending(o => o.CreationTime)
            .Take(500)
            .Select(o => new
            {
                o.CustomerPhone,
                o.CardcomTokenLast4,
                o.CardcomCardBrand,
                o.CustomerPaymentMethodId,
                o.PaymentMethod,
                o.PaymentSettleStatus,
            })
            .ToListAsync(cancelToken);

        foreach (var o in recentOrders.Where(o => NormalizePhoneDigits(o.CustomerPhone) == normalized))
        {
            if (o.CustomerPaymentMethodId is int pmId)
            {
                var pm = await GetPaymentMethodByIdAsync(pmId, cancelToken);
                if (pm != null && !pm.IsRetired)
                {
                    return (
                        true,
                        pm.Last4Digits ?? o.CardcomTokenLast4,
                        pm.CardBrand ?? o.CardcomCardBrand,
                        pm.Id);
                }
            }

            if (!IsCardcomCreditPaymentMethod(o.PaymentMethod))
                continue;

            var hasTokenOnOrder = o.CustomerPaymentMethodId != null
                || !string.IsNullOrWhiteSpace(o.CardcomTokenLast4)
                || string.Equals(o.PaymentSettleStatus, "Authorized", StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.PaymentSettleStatus, "Captured", StringComparison.OrdinalIgnoreCase);

            if (!hasTokenOnOrder)
                continue;

            return (true, o.CardcomTokenLast4, o.CardcomCardBrand, o.CustomerPaymentMethodId);
        }

        return null;
    }
}
