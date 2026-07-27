using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

public class CustomerService : ServiceBase
{
    private readonly CustomerStorage _customerStorage;
    private readonly OrderService _orderService;
    private readonly IIntegrationLogQueue _integrationLogQueue;
    private readonly IntegrationLogStorage _integrationLogStorage;
    private readonly UserStorage _userStorage;

    public CustomerService(
        ILogger<CustomerService> logger,
        IMapper mapper,
        CacheManager cache,
        CustomerStorage customerStorage,
        OrderService orderService,
        IIntegrationLogQueue integrationLogQueue,
        IntegrationLogStorage integrationLogStorage,
        UserStorage userStorage)
        : base(logger, mapper, cache)
    {
        _customerStorage = customerStorage;
        _orderService = orderService;
        _integrationLogQueue = integrationLogQueue;
        _integrationLogStorage = integrationLogStorage;
        _userStorage = userStorage;
    }

    /// <summary>Build the customer's activity timeline (newest first) from IntegrationLog rows, resolving actor names.</summary>
    private async Task<List<CustomerActivityItem>> BuildActivityAsync(int customerId, CancellationToken cancelToken)
    {
        var logs = await _integrationLogStorage.GetForEntityAsync(CustomerActivityLog.EntityTypeCustomer, customerId, cancelToken).ConfigureAwait(false);
        if (logs.Count == 0) return new List<CustomerActivityItem>();
        var capped = logs.Take(50).ToList();

        // Resolve actor names once per distinct user id.
        var actorIds = capped
            .Select(l => CustomerActivityLog.ParsePayload(l.RequestJson)?.ActorUserId)
            .Where(id => id is > 0).Select(id => id!.Value).Distinct().ToList();
        var names = new Dictionary<int, string?>();
        foreach (var uid in actorIds)
            names[uid] = await _userStorage.GetUserNameAsync(uid, cancelToken).ConfigureAwait(false);

        return capped.Select(l =>
        {
            var p = CustomerActivityLog.ParsePayload(l.RequestJson);
            return new CustomerActivityItem
            {
                Id = l.Id.ToString(),
                Title = p?.Title ?? l.Operation,
                Subtitle = p?.Subtitle,
                Date = ToUtcIsoString(l.CreatedAtUtc),
                Type = l.Operation,
                ActorName = p?.ActorUserId is int uid && names.TryGetValue(uid, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null,
            };
        }).ToList();
    }

    /// <summary>Cancel all of a customer's active orders (same side effects as a single cancel: stock restore, promotion-metric reversal, soft-delete).</summary>
    private async Task CancelCustomerOrdersAsync(int customerId, int? siteId, CancellationToken cancelToken)
    {
        var orderIds = await _customerStorage.GetActiveOrderIdsByCustomerAsync(customerId, siteId, cancelToken).ConfigureAwait(false);
        if (orderIds.Count == 0) return;
        // The action filter only sets AuthUser on this service — forward it so cancels record the right actor.
        _orderService.AuthUser = AuthUser;
        foreach (var orderId in orderIds)
            await _orderService.CancelOrderAsync(orderId, softDelete: true, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Serialize DateTime as ISO 8601 with Z so clients display correct local time.</summary>
    private static string ToUtcIsoString(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("o");
    }

    private static CustomerRes MapListRowToRes(CustomerStorage.CustomerListRow row)
    {
        var c = row.Customer;
        return new CustomerRes
        {
            Id = c.Id,
            Name = c.Name ?? "",
            Email = c.Email,
            Phone = c.Phone,
            City = c.City,
            Notes = c.Notes,
            OrderCount = row.OrderCountAtSite,
            TotalRevenue = row.TotalRevenueAtSite,
            MarketingApproval = c.MarketingApproval,
            CreatedAt = ToUtcIsoString(c.CreationTime),
            IsReturning = row.OrderCountAtSite > 1,
            LastOrderId = row.LastOrderIdAtSite,
            LastOrderDate = row.LastOrderAtSite.HasValue ? ToUtcIsoString(row.LastOrderAtSite.Value) : null,
            ChurnThresholdDays = row.ChurnThresholdDays,
            IsActive = row.IsActive,
            IsAtRisk = row.IsAtRisk
        };
    }

    private static List<string>? BuildAddressLinesFromCustomer(Customer c)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.DeliveryStreet))
            lines.Add(c.DeliveryStreet.Trim());
        if (!string.IsNullOrWhiteSpace(c.City))
            lines.Add(c.City.Trim());
        var tail = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.DeliveryApartment))
            tail.Add(c.DeliveryApartment.Trim());
        if (!string.IsNullOrWhiteSpace(c.DeliveryFloor))
            tail.Add(c.DeliveryFloor.Trim());
        if (!string.IsNullOrWhiteSpace(c.DeliveryEntranceCode))
            tail.Add(c.DeliveryEntranceCode.Trim());
        if (tail.Count > 0)
            lines.Add(string.Join(" · ", tail));
        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(c.DefaultAddress))
            lines.Add(c.DefaultAddress.Trim());
        return lines.Count == 0 ? null : lines;
    }

    private static CustomerDetailRes MapCustomerToDetailRes(Customer c, int orderCount, decimal totalRevenue, int? lastOrderId, DateTime? lastOrderAt, int averageReturnDays)
    {
        return new CustomerDetailRes
        {
            Id = c.Id,
            Name = c.Name ?? "",
            Email = c.Email,
            Phone = c.Phone,
            City = c.City,
            Notes = c.Notes,
            OrderCount = orderCount,
            TotalRevenue = totalRevenue,
            MarketingApproval = c.MarketingApproval,
            CreatedAt = ToUtcIsoString(c.CreationTime),
            IsReturning = orderCount > 1,
            LastOrderId = lastOrderId,
            DefaultAddress = c.DefaultAddress,
            DeliveryStreet = c.DeliveryStreet,
            DeliveryApartment = c.DeliveryApartment,
            DeliveryFloor = c.DeliveryFloor,
            DeliveryEntranceCode = c.DeliveryEntranceCode,
            AddressLines = BuildAddressLinesFromCustomer(c),
            MarketingEmail = c.MarketingEmail,
            MarketingSms = c.MarketingSms,
            LastOrderDate = lastOrderAt?.ToString("dd/MM/yyyy"),
            AverageReturnDays = averageReturnDays,
            Activity = new List<CustomerActivityItem>(),
            PermanentDiscountType = c.PermanentDiscountType,
            PermanentDiscountValue = c.PermanentDiscountValue,
        };
    }

    public async Task<IApiResponse<ApiListResponse<CustomerRes>>> GetCustomersAsync(
        ApiListReq<CustomerFilter> request,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<ApiListResponse<CustomerRes>>
        {
            Data = new ApiListResponse<CustomerRes>()
        };

        var paging = new PagingExDto
        {
            Skip = request.Skip,
            Take = request.Take,
            IncludeTotal = true
        };
        var res = await _customerStorage.GetCustomersAsync(request.Filter ?? new CustomerFilter(), paging, cancelToken).ConfigureAwait(false);

        response.Data!.Items = res.Items.Select(MapListRowToRes).ToList();
        response.Data.Skip = request.Skip;
        response.Data.Limit = request.Take;
        response.Data.Total = res.Total;

        return response;
    }

    public async Task<IApiResponse<CustomerStatsRes>> GetStatsAsync(int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerStatsRes> { Data = new CustomerStatsRes() };
        var dto = await _customerStorage.GetCustomerStatsAsync(siteId, cancelToken).ConfigureAwait(false);
        response.Data!.TotalCustomers = dto.TotalCustomers;
        response.Data.ActiveCustomers = dto.ActiveCustomers;
        response.Data.ActiveCustomersTrendPercent = dto.ActiveCustomersTrendPercent;
        response.Data.Aov = dto.Aov;
        response.Data.AovTrendPercent = dto.AovTrendPercent;
        response.Data.AverageOrdersPerCustomer = dto.AverageOrdersPerCustomer;
        response.Data.AverageOrdersPerCustomerTrend = dto.AverageOrdersPerCustomerTrend;
        response.Data.AverageReturnDays = dto.AverageReturnDays;
        response.Data.AverageReturnDaysTrend = dto.AverageReturnDaysTrend;
        response.Data.AtRiskCustomers = dto.AtRiskCustomers;
        response.Data.AtRiskCustomersTrendPercent = dto.AtRiskCustomersTrendPercent;
        response.Data.ChurnThresholdDays = dto.ChurnThresholdDays;
        response.Data.HasComparison = dto.HasComparison;
        response.Data.ReturningCustomersPercent = dto.ReturningCustomersPercent;
        return response;
    }

    public async Task<IApiResponse<CustomerDetailRes>> CreateCustomerAsync(int? siteId, CustomerCreateReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerDetailRes>();
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return CreateResponse(response, StatusCode.InvalidRequest, "Name is required");
        if (siteId is not int sid || sid <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required");

        var actorId = AuthUser.Id;
        var created = await _customerStorage.CreateCustomerAsync(
            sid,
            req.Name.Trim(),
            string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim(),
            string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            onCreated: c => _integrationLogQueue.TryEnqueue(
                CustomerActivityLog.Build(sid, c.Id, CustomerActivityLog.OpCreated, "הלקוח נוצר", "נוצר דרך ממשק הניהול", actorId)),
            cancelToken: cancelToken).ConfigureAwait(false);
        if (created == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
        return await GetCustomerAsync(created.Id, sid, cancelToken).ConfigureAwait(false);
    }

    public async Task<IApiResponse<CustomerImportRes>> ImportCustomersAsync(int? siteId, CustomerImportReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerImportRes>();
        if (siteId is not int sid || sid <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required");
        if (req == null || req.Rows.Count == 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "No rows to import");

        var actorId = AuthUser.Id;
        var rows = req.Rows.Select(r => new CustomerStorage.ImportRow
        {
            Name = r.Name?.Trim() ?? "",
            Phone = r.Phone,
            Email = r.Email,
            City = r.City,
            DeliveryStreet = r.DeliveryStreet,
            DeliveryApartment = r.DeliveryApartment,
            DeliveryFloor = r.DeliveryFloor,
            DeliveryEntranceCode = r.DeliveryEntranceCode,
            Notes = r.Notes,
            MarketingApproval = r.MarketingApproval
        }).ToList();

        var result = await _customerStorage.ImportCustomersAsync(
            sid,
            rows,
            onCreated: c => _integrationLogQueue.TryEnqueue(
                CustomerActivityLog.Build(sid, c.Id, CustomerActivityLog.OpCreated, "הלקוח נוצר", "יובא מקובץ לקוחות", actorId)),
            cancelToken: cancelToken).ConfigureAwait(false);
        if (result == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");

        response.Data = new CustomerImportRes
        {
            Total = result.Total,
            Created = result.Created,
            Updated = result.Updated,
            Skipped = result.Skipped,
            Failed = result.Failed,
            Issues = result.Issues.Select(i => new CustomerImportRowIssueRes
            {
                Name = i.Name,
                Phone = i.Phone,
                Email = i.Email,
                Status = i.Status,
                Reason = i.Reason
            }).ToList()
        };
        return response;
    }

    public async Task<IApiResponse<CustomerDetailRes>> GetCustomerAsync(int id, int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerDetailRes>();
        var customer = await _customerStorage.GetCustomerByIdAsync(id, siteId, cancelToken).ConfigureAwait(false);
        if (customer == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        var (orderCount, totalRevenue, lastOrderId, lastOrderAt, averageReturnDays) = await _customerStorage.GetCustomerGlobalStatsAsync(id, cancelToken).ConfigureAwait(false);
        response.Data = MapCustomerToDetailRes(customer, orderCount, totalRevenue, lastOrderId, lastOrderAt, averageReturnDays);
        response.Data.Activity = await BuildActivityAsync(id, cancelToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>Add a manual activity note to the customer timeline (records the acting user + time). Written synchronously so it shows immediately.</summary>
    public async Task<IApiResponse<CustomerDetailRes>> AddActivityNoteAsync(int id, int? siteId, string? text, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerDetailRes>();
        var note = text?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            return CreateResponse(response, StatusCode.InvalidRequest, "Note text is required");
        var customer = await _customerStorage.GetCustomerByIdAsync(id, siteId, cancelToken).ConfigureAwait(false);
        if (customer == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        var row = CustomerActivityLog.Build(customer.SiteId, id, CustomerActivityLog.OpNote, note!, null, AuthUser.Id);
        try { await _integrationLogStorage.AddAsync(row, cancelToken).ConfigureAwait(false); }
        catch { /* timeline write must not break the request */ }
        return await GetCustomerAsync(id, siteId, cancelToken).ConfigureAwait(false);
    }

    public async Task<IApiResponse<CustomerLastOrderRes?>> GetLastOrderAsync(int id, int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerLastOrderRes?> { Data = null };
        var order = await _customerStorage.GetLastOrderByCustomerIdAsync(id, siteId, cancelToken).ConfigureAwait(false);
        if (order == null) return response;
        var firstItem = order.OrderItem?.OrderBy(i => i.SortOrder).FirstOrDefault();
        response.Data = new CustomerLastOrderRes
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber ?? order.Id.ToString(),
            Status = order.Status ?? "",
            PaymentStatus = order.PaymentStatus ?? "",
            CreatedAt = ToUtcIsoString(order.CreationTime),
            Total = order.Total ?? 0,
            Source = order.Source,
            FirstItemTitle = firstItem?.Title,
            ItemCount = order.OrderItem?.Count ?? 0
        };
        return response;
    }

    public async Task<IApiResponse<CustomerDetailRes>> UpdateCustomerAsync(int id, int? siteId, CustomerUpdateReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<CustomerDetailRes>();
        if (req == null || string.IsNullOrWhiteSpace(req.Name))
            return CreateResponse(response, StatusCode.InvalidRequest, "Name is required");
        var (updated, phoneConflict) = await _customerStorage.UpdateCustomerAsync(
            id, siteId, req.Name.Trim(), req.Notes, req.Email, req.Phone, req.City,
            req.DeliveryStreet, req.DeliveryApartment, req.DeliveryFloor, req.DeliveryEntranceCode,
            req.MarketingEmail, req.MarketingSms, cancelToken).ConfigureAwait(false);
        if (phoneConflict)
            return CreateResponse(response, StatusCode.InvalidRequest, "Phone number already in use by another customer");
        if (updated == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return await GetCustomerAsync(id, siteId, cancelToken).ConfigureAwait(false);
    }

    public async Task<IApiResponse<bool>> DeleteCustomerAsync(int id, int? siteId, bool withOrders, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool> { Data = true };
        if (withOrders)
            await CancelCustomerOrdersAsync(id, siteId, cancelToken).ConfigureAwait(false);
        var deleted = await _customerStorage.DeleteCustomerAsync(id, siteId, cancelToken).ConfigureAwait(false);
        if (!deleted)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return response;
    }

    public async Task<IApiResponse<bool>> DeleteManyAsync(DeleteManyCustomerReq req, int? siteId, bool withOrders, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool> { Data = true };
        if (req?.Ids != null && req.Ids.Count > 0)
        {
            if (withOrders)
                foreach (var id in req.Ids)
                    await CancelCustomerOrdersAsync(id, siteId, cancelToken).ConfigureAwait(false);
            await _customerStorage.DeleteCustomersAsync(req.Ids, siteId, cancelToken).ConfigureAwait(false);
        }
        return response;
    }
}
