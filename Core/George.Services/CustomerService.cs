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

    public CustomerService(
        ILogger<CustomerService> logger,
        IMapper mapper,
        CacheManager cache,
        CustomerStorage customerStorage)
        : base(logger, mapper, cache)
    {
        _customerStorage = customerStorage;
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
            LastOrderId = row.LastOrderIdAtSite
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
            Activity = new List<CustomerActivityItem>()
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
        response.Data.ReturningCustomersPercent = dto.ReturningCustomersPercent;
        response.Data.AverageReturnDays = dto.AverageReturnDays;
        response.Data.AverageOrdersPerCustomer = dto.AverageOrdersPerCustomer;
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
        return response;
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
        var updated = await _customerStorage.UpdateCustomerAsync(id, siteId, req.Name.Trim(), cancelToken).ConfigureAwait(false);
        if (updated == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return await GetCustomerAsync(id, siteId, cancelToken).ConfigureAwait(false);
    }

    public async Task<IApiResponse<bool>> DeleteCustomerAsync(int id, int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool> { Data = true };
        var deleted = await _customerStorage.DeleteCustomerAsync(id, siteId, cancelToken).ConfigureAwait(false);
        if (!deleted)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return response;
    }

    public async Task<IApiResponse<bool>> DeleteManyAsync(DeleteManyCustomerReq req, int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool> { Data = true };
        if (req?.Ids != null && req.Ids.Count > 0)
            await _customerStorage.DeleteCustomersAsync(req.Ids, siteId, cancelToken).ConfigureAwait(false);
        return response;
    }
}
