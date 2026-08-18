using George.Common;
using George.Data;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace George.Services;

/// <summary>Sprint 2: Order reception open/close per site (פתיחה/סגירת קבלת הזמנות).</summary>
public class OrderReceptionService : ServiceBase
{
    private readonly OrderReceptionStorage _storage;
    private readonly SiteStorage _siteStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderReceptionService(
        ILogger<OrderReceptionService> logger,
        AutoMapper.IMapper mapper,
        CacheManager cache,
        OrderReceptionStorage storage,
        SiteStorage siteStorage,
        IHttpClientFactory httpClientFactory)
        : base(logger, mapper, cache)
    {
        _storage = storage;
        _siteStorage = siteStorage;
        _httpClientFactory = httpClientFactory;
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

        var result = await GetAsync(siteId, cancelToken).ConfigureAwait(false);

        // Push to WooCommerce (oc-storeos) — best-effort, does not fail the save
        try
        {
            await SyncClosingDatesToWooCommerceAsync(siteId, result.Data!, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WooCommerce closing-dates sync failed (non-fatal). siteId={SiteId}", siteId);
        }

        return result;
    }

    /// <summary>
    /// Pushes the current closing dates to the WooCommerce oc-storeos API:
    /// POST <c>{WooCommerceUrl}/wp-json/oc-storeos/v1/default-closing-dates</c>.
    /// Two calls: one for "shipping" (delivery) and one for "pickup".
    /// </summary>
    private async Task SyncClosingDatesToWooCommerceAsync(int siteId, OrderReceptionRes state, CancellationToken cancelToken)
    {
        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null || site.WooCommerceEnabled != true) return;

        var ocV1Base = OcStoreosApiUrls.V1BaseFromWooCommerceRoot(site.WooCommerceUrl);
        if (string.IsNullOrWhiteSpace(ocV1Base)) return;

        var endpoint = $"{ocV1Base}/default-closing-dates";

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        httpClient.DefaultRequestHeaders.Clear();

        // oc-storeos is a custom REST namespace: WooCommerce ck/cs Basic auth is only honored on wc/*
        // routes, so WordPress treats the consumer key as an application-password username and rejects
        // the whole request with 401 invalid_username before the route's permission callback runs.
        // Authenticate like the other oc-storeos/ed calls instead: shared token via X-Api-Key.
        var apiToken = site.InternalApiKey?.Trim();
        if (!string.IsNullOrEmpty(apiToken))
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiToken);

        var today = DateTime.UtcNow.Date;

        // Build delivery (shipping) date list: today + future dates
        var deliveryDates = BuildWooDates(today, state.TodayDeliveryClosed, state.FutureDeliveryDates);
        await PostClosingDatesAsync(httpClient, endpoint, "shipping", deliveryDates, siteId, cancelToken).ConfigureAwait(false);

        // Build pickup date list: today + future dates
        var pickupDates = BuildWooDates(today, state.TodayPickupClosed, state.FuturePickupDates);
        await PostClosingDatesAsync(httpClient, endpoint, "pickup", pickupDates, siteId, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Combines today (if closed) with future dates, converting from yyyy-MM-dd to dd/MM/yyyy for the WooCommerce API.</summary>
    private static List<string> BuildWooDates(DateTime today, bool todayClosed, List<string> futureDates)
    {
        var result = new List<string>();
        if (todayClosed)
            result.Add(today.ToString("dd/MM/yyyy"));
        foreach (var d in futureDates)
        {
            if (DateTime.TryParse(d, out var dt))
                result.Add(dt.ToString("dd/MM/yyyy"));
        }
        return result;
    }

    private async Task PostClosingDatesAsync(
        HttpClient httpClient,
        string endpoint,
        string type,
        List<string> dates,
        int siteId,
        CancellationToken cancelToken)
    {
        var payload = new { type, dates };
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "WooCommerce closing-dates POST. siteId={SiteId}, type={Type}, dateCount={DateCount}, endpoint={Endpoint}",
            siteId, type, dates.Count, endpoint);

        var response = await httpClient.PostAsync(endpoint, content, cancelToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "WooCommerce closing-dates POST succeeded. siteId={SiteId}, type={Type}, httpStatus={HttpStatus}",
                siteId, type, (int)response.StatusCode);
        }
        else
        {
            var err = await response.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            _logger.LogWarning(
                "WooCommerce closing-dates POST failed. siteId={SiteId}, type={Type}, httpStatus={HttpStatus}, error={Error}",
                siteId, type, (int)response.StatusCode, err);
        }
    }
}
