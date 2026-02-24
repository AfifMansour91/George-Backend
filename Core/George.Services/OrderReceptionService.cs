using George.Common;
using George.Data;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>Sprint 2: Order reception open/close per site (פתיחה/סגירת קבלת הזמנות).</summary>
public class OrderReceptionService : ServiceBase
{
    private readonly OrderReceptionStorage _storage;

    public OrderReceptionService(
        ILogger<OrderReceptionService> logger,
        AutoMapper.IMapper mapper,
        CacheManager cache,
        OrderReceptionStorage storage)
        : base(logger, mapper, cache)
    {
        _storage = storage;
    }

    public async Task<IApiResponse<OrderReceptionRes>> GetAsync(int siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderReceptionRes>();
        var data = await _storage.GetForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        response.Data = new OrderReceptionRes
        {
            SiteId = data.SiteId,
            TodayDeliveryClosed = data.TodayDeliveryClosed,
            TodayPickupClosed = data.TodayPickupClosed,
            FutureDeliveryDates = data.FutureDeliveryDates.ToList(),
            FuturePickupDates = data.FuturePickupDates.ToList()
        };
        return response;
    }

    public async Task<IApiResponse<OrderReceptionRes>> SaveAsync(int siteId, OrderReceptionReq req, CancellationToken cancelToken = default)
    {
        var update = new OrderReceptionUpdateData
        {
            TodayDeliveryClosed = req.TodayDeliveryClosed,
            TodayPickupClosed = req.TodayPickupClosed,
            FutureDeliveryDates = req.FutureDeliveryDates,
            FuturePickupDates = req.FuturePickupDates
        };
        await _storage.SaveForSiteAsync(siteId, update, cancelToken).ConfigureAwait(false);
        return await GetAsync(siteId, cancelToken).ConfigureAwait(false);
    }
}
