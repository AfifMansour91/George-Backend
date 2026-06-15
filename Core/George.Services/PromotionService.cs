using AutoMapper;
using George.Common;
using George.Data;
using George.Data.Models;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

public class PromotionService : ServiceBase
{
    private readonly PromotionStorage _promotionStorage;
    private readonly SiteStorage _siteStorage;
    private readonly CustomerStorage _customerStorage;
    private readonly ProductStorage _productStorage;
    private readonly PromotionWebhookDispatcher _webhooks;

    public PromotionService(
        ILogger<PromotionService> logger,
        IMapper mapper,
        CacheManager cache,
        PromotionStorage promotionStorage,
        SiteStorage siteStorage,
        CustomerStorage customerStorage,
        ProductStorage productStorage,
        PromotionWebhookDispatcher webhooks)
        : base(logger, mapper, cache)
    {
        _promotionStorage = promotionStorage;
        _siteStorage = siteStorage;
        _customerStorage = customerStorage;
        _productStorage = productStorage;
        _webhooks = webhooks;
    }

    public async Task<IApiResponse<ApiListResponse<PromotionRes>>> GetPromotionsAsync(
        ApiListReq<PromotionFilter> request,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<ApiListResponse<PromotionRes>> { Data = new ApiListResponse<PromotionRes>() };

        var site = await _siteStorage.GetSiteAsync(request.Filter.SiteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var res = await _promotionStorage.GetPromotionsAsync(request.Filter, request, cancelToken).ConfigureAwait(false);
        response.Data!.Items = res.Items.ConvertAll(row =>
        {
            var dto = _mapper.Map<PromotionRes>(row.Promotion);
            dto.PeriodRedemptions = row.PeriodRedemptions;
            dto.PeriodRevenueNis = row.PeriodRevenueNis;
            dto.PeriodDiscountNis = row.PeriodDiscountNis;
            return dto;
        });
        response.Data.Skip = request.Skip;
        response.Data.Limit = request.Take;
        response.Data.Total = res.Total;
        return response;
    }

    public async Task<IApiResponse<PromotionStatsRes>> GetPromotionStatsAsync(
        PromotionStatsFilter filter,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionStatsRes> { Data = new PromotionStatsRes() };

        var site = await _siteStorage.GetSiteAsync(filter.SiteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var tabs = await _promotionStorage.GetPromotionTabCountsAsync(filter.SiteId, cancelToken).ConfigureAwait(false);
        var ending = await _promotionStorage.GetActivePromotionsEndingWithinWeekAsync(filter.SiteId, cancelToken).ConfigureAwait(false);

        var toDate = filter.PeriodToUtc?.Date ?? DateTime.UtcNow.Date;
        var fromDate = filter.PeriodFromUtc?.Date ?? toDate.AddDays(-30);
        var fromInstant = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
        var toExclusive = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Utc);

        var totals = await _promotionStorage.GetPromotionPeriodTotalsAsync(
                filter.SiteId,
                fromInstant,
                DateTime.SpecifyKind(toDate, DateTimeKind.Utc),
                filter.Channel,
                filter.DiscountKind,
                cancelToken)
            .ConfigureAwait(false);

        var siteOrders = await _promotionStorage.GetSiteOrderTotalForPeriodAsync(filter.SiteId, fromInstant, toExclusive, cancelToken)
            .ConfigureAwait(false);

        response.Data!.TabAll = tabs.All;
        response.Data.TabActive = tabs.Active;
        response.Data.TabScheduled = tabs.Scheduled;
        response.Data.TabDrafts = tabs.Drafts;
        response.Data.TabEnded = tabs.Ended;
        response.Data.EndingWithinWeek = ending;
        response.Data.PeriodRevenueNis = totals.RevenueNis;
        response.Data.PeriodDiscountNis = totals.DiscountNis;
        response.Data.PeriodRedemptions = totals.Redemptions;
        response.Data.YieldPerDiscountNis = totals.DiscountNis > 0m
            ? decimal.Round(totals.RevenueNis / totals.DiscountNis, 2, MidpointRounding.AwayFromZero)
            : 0m;
        response.Data.RevenuePctOfSiteOrders = siteOrders > 0m
            ? (int?)decimal.ToInt32(decimal.Round(totals.RevenueNis / siteOrders * 100m, 0, MidpointRounding.AwayFromZero))
            : null;

        return response;
    }

    public async Task<IApiResponse<PromotionRes>> GetPromotionAsync(int promotionId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionRes>();
        var p = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        if (p == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        response.Data = _mapper.Map<PromotionRes>(p);
        return response;
    }

    public async Task<IApiResponse<PromotionRes>> CreatePromotionAsync(CreatePromotionReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionRes>();
        var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (req.ScheduleStartDateUtc.HasValue && req.ScheduleEndDateUtc.HasValue
            && req.ScheduleStartDateUtc.Value.Date > req.ScheduleEndDateUtc.Value.Date)
            return CreateResponse(response, StatusCode.InvalidRequest, "Schedule end date must be on or after start date.");

        var typeNorm = PromotionWire.PromotionType.NormalizeOrNull(req.PromotionType);
        if (typeNorm == null)
            return CreateResponse(response, StatusCode.InvalidRequest, "PromotionType must be discount, buy_x_pay_y, or buy_x_get_y.");

        var payloadJson = string.IsNullOrWhiteSpace(req.PayloadJson) ? "{}" : req.PayloadJson.Trim();
        var listKind = string.IsNullOrWhiteSpace(req.ListDiscountKind) ? null : req.ListDiscountKind.Trim().ToLowerInvariant();
        if (!PromotionPayloadValidator.TryValidate(typeNorm, payloadJson, isDraft: false, listKind, out var payloadError))
            return CreateResponse(response, StatusCode.InvalidRequest, payloadError ?? "Invalid promotion payload.");

        var couponNorm = NormalizeCouponCode(req.CouponCode);
        if (couponNorm != null && await _promotionStorage.PromotionCouponCodeExistsAsync(req.SiteId, couponNorm, null, cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.DBDuplicateKeyViolation, "Coupon code is already in use for this site.");

        var model = _mapper.Map<Promotion>(req);
        model.PromotionType = typeNorm;
        model.PayloadJson = payloadJson;
        if (string.IsNullOrWhiteSpace(model.ChannelsJson))
            model.ChannelsJson = PromotionWire.DefaultChannelsJson;
        if (model.GuidId == Guid.Empty)
            model.GuidId = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(model.ListDiscountKind))
        {
            model.ListDiscountKind = typeNorm switch
            {
                PromotionWire.PromotionType.Discount => PromotionWire.DiscountKind.Percent,
                PromotionWire.PromotionType.BuyXPayY => PromotionWire.DiscountKind.Bxpy,
                PromotionWire.PromotionType.BuyXGetY => PromotionWire.DiscountKind.Bxgy,
                _ => PromotionWire.DiscountKind.Percent,
            };
        }

        model.CouponCode = string.IsNullOrWhiteSpace(req.CouponCode) ? null : req.CouponCode.Trim();
        model.IsDraft = false;
        model.CreationUserId = AuthUser.Id;
        model = await _promotionStorage.CreatePromotionAsync(model, cancelToken).ConfigureAwait(false);
        var reloaded = await _promotionStorage.GetPromotionAsync(model.Id, cancelToken).ConfigureAwait(false);
        response.Data = _mapper.Map<PromotionRes>(reloaded!);

        // Webhook: only fire for published promotions; drafts are still being authored.
        // See `Sprint4/מבצעים.md` "סנכרון מבצעים לאתר ולקיוסק".
        if (reloaded != null)
        {
            var siteForHook = await _siteStorage.GetSiteAsync(reloaded.SiteId, cancelToken).ConfigureAwait(false);
            await _webhooks.FireAsync(PromotionWebhookDispatcher.EventCreated, reloaded, siteForHook, cancelToken).ConfigureAwait(false);
        }
        return response;
    }

    public async Task<IApiResponse<PromotionRes>> UpdatePromotionAsync(int promotionId, UpdatePromotionReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionRes>();
        var existing = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        if (existing == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        string mergedType;
        if (!string.IsNullOrWhiteSpace(req.PromotionType))
        {
            var tn = PromotionWire.PromotionType.NormalizeOrNull(req.PromotionType);
            if (tn == null)
                return CreateResponse(response, StatusCode.InvalidRequest, "PromotionType must be discount, buy_x_pay_y, or buy_x_get_y.");
            mergedType = tn;
        }
        else
            mergedType = existing.PromotionType;

        var mergedPayload = req.PayloadJson != null
            ? (string.IsNullOrWhiteSpace(req.PayloadJson) ? "{}" : req.PayloadJson.Trim())
            : existing.PayloadJson;
        var mergedIsDraft = false;
        var mergedListKind = !string.IsNullOrWhiteSpace(req.ListDiscountKind)
            ? req.ListDiscountKind.Trim().ToLowerInvariant()
            : existing.ListDiscountKind;

        if (!PromotionPayloadValidator.TryValidate(mergedType, mergedPayload, mergedIsDraft, mergedListKind, out var payloadError))
            return CreateResponse(response, StatusCode.InvalidRequest, payloadError ?? "Invalid promotion payload.");

        var mergedCoupon = req.CouponCode != null
            ? NormalizeCouponCode(req.CouponCode)
            : NormalizeCouponCode(existing.CouponCode);
        if (mergedCoupon != null && await _promotionStorage.PromotionCouponCodeExistsAsync(existing.SiteId, mergedCoupon, promotionId, cancelToken).ConfigureAwait(false))
            return CreateResponse(response, StatusCode.DBDuplicateKeyViolation, "Coupon code is already in use for this site.");

        var mergedStart = req.ScheduleStartDateUtc ?? existing.ScheduleStartDateUtc;
        var mergedEnd = req.ScheduleEndDateUtc ?? existing.ScheduleEndDateUtc;
        if (mergedStart.HasValue && mergedEnd.HasValue && mergedStart.Value.Date > mergedEnd.Value.Date)
            return CreateResponse(response, StatusCode.InvalidRequest, "Schedule end date must be on or after start date.");

        var updated = await _promotionStorage.UpdatePromotionAsync(promotionId, db =>
        {
            if (!string.IsNullOrWhiteSpace(req.PromotionType))
                db.PromotionType = mergedType;
            if (!string.IsNullOrWhiteSpace(req.Name))
                db.Name = req.Name.Trim();
            if (req.IsActive.HasValue) db.IsActive = req.IsActive.Value;
            if (req.ShowBadge.HasValue) db.ShowBadge = req.ShowBadge.Value;
            db.IsDraft = false;
            if (req.ScheduleStartDateUtc.HasValue) db.ScheduleStartDateUtc = req.ScheduleStartDateUtc;
            if (req.ScheduleEndDateUtc.HasValue) db.ScheduleEndDateUtc = req.ScheduleEndDateUtc;
            if (req.PayloadJson != null) db.PayloadJson = mergedPayload;
            if (!string.IsNullOrWhiteSpace(req.ListDiscountKind))
                db.ListDiscountKind = req.ListDiscountKind.Trim();
            if (req.ChannelsJson != null) db.ChannelsJson = string.IsNullOrWhiteSpace(req.ChannelsJson) ? PromotionWire.DefaultChannelsJson : req.ChannelsJson;
            if (req.CouponCode != null) db.CouponCode = string.IsNullOrWhiteSpace(req.CouponCode) ? null : req.CouponCode.Trim();
            if (req.AppliesToSummary != null) db.AppliesToSummary = string.IsNullOrWhiteSpace(req.AppliesToSummary) ? null : req.AppliesToSummary.Trim();
            if (req.Priority.HasValue) db.Priority = Math.Max(0, req.Priority.Value);
            db.UpdateUserId = AuthUser.Id;
        }, cancelToken).ConfigureAwait(false);

        if (updated == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var reloaded = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        response.Data = _mapper.Map<PromotionRes>(reloaded!);

        // Webhook for full updates.
        if (reloaded != null)
        {
            var siteForHook = await _siteStorage.GetSiteAsync(reloaded.SiteId, cancelToken).ConfigureAwait(false);
            await _webhooks.FireAsync(PromotionWebhookDispatcher.EventUpdated, reloaded, siteForHook, cancelToken).ConfigureAwait(false);
        }
        return response;
    }

    public async Task<IApiResponse<PromotionRes>> UpdatePromotionStatusAsync(int promotionId, UpdatePromotionStatusReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionRes>();
        var updated = await _promotionStorage.UpdatePromotionAsync(promotionId, db =>
        {
            db.IsActive = req.IsActive;
            db.UpdateUserId = AuthUser.Id;
        }, cancelToken).ConfigureAwait(false);

        if (updated == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var reloaded = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        response.Data = _mapper.Map<PromotionRes>(reloaded!);

        if (reloaded != null)
        {
            var siteForHook = await _siteStorage.GetSiteAsync(reloaded.SiteId, cancelToken).ConfigureAwait(false);
            await _webhooks.FireAsync(PromotionWebhookDispatcher.EventUpdated, reloaded, siteForHook, cancelToken).ConfigureAwait(false);
        }
        return response;
    }

    public async Task<IApiResponse<object?>> DeletePromotionAsync(int promotionId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<object?>();
        var existing = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        if (existing == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var site = await _siteStorage.GetSiteAsync(existing.SiteId, cancelToken).ConfigureAwait(false);
        await _webhooks.FireDeleteAsync(existing, site, cancelToken).ConfigureAwait(false);

        var ok = await _promotionStorage.DeletePromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
        if (!ok)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return response;
    }

    /// <summary>
    /// `POST /Promotion/evaluate` — runs the cart-eligibility + math engine
    /// (<see cref="PromotionEvaluator"/>) for the given site and cart. Returns the list
    /// of applied promotions and the total discount, per <c>Sprint4/מבצעים.md</c>.
    /// </summary>
    public async Task<IApiResponse<EvaluatePromotionsRes>> EvaluatePromotionsAsync(
        EvaluatePromotionsReq req, CancellationToken cancelToken)
    {
        var response = new ApiResponse<EvaluatePromotionsRes>
        {
            Data = new EvaluatePromotionsRes(),
        };
        if (req.SiteId <= 0)
            return CreateResponse(response, StatusCode.InvalidId);

        cancelToken.ThrowIfCancellationRequested();
        var utcNow = DateTime.UtcNow;
        var candidates = await _promotionStorage
            .GetActivePromotionsForEvaluationAsync(req.SiteId, utcNow, cancelToken)
            .ConfigureAwait(false);

        if (candidates.Count == 0 || req.Cart is null || req.Cart.Count == 0)
            return response;

        // Site-level defaults for overage policy / phone-orders / discounted-product gating.
        var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken).ConfigureAwait(false);
        var defaults = new PromotionEvaluator.SiteEvaluationDefaults
        {
            OveragePolicyDefault = string.IsNullOrWhiteSpace(site?.PromotionOveragePolicyDefault)
                ? "full_price"
                : site!.PromotionOveragePolicyDefault!,
            ApplyOnPhoneOrders = site?.PromotionsApplyToPhoneOrders ?? true,
            ApplyOnDiscountedProducts = site?.PromotionsApplyToDiscountedProducts ?? false,
        };

        // Coupon is normalized inside the evaluator; re-using NormalizeCouponCode keeps the
        // create/update + evaluate paths consistent.
        if (!string.IsNullOrWhiteSpace(req.CouponCode))
            req.CouponCode = NormalizeCouponCode(req.CouponCode);

        if (!defaults.ApplyOnDiscountedProducts && req.Cart is { Count: > 0 })
            await EnrichCatalogDiscountFlagsAsync(req.Cart, utcNow, cancelToken).ConfigureAwait(false);

        var customerId = await ResolveCustomerIdForEvaluateAsync(req, cancelToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, int>? priorRedemptions = null;
        if (customerId > 0)
        {
            var promoIds = candidates.Select(p => p.Id).ToList();
            priorRedemptions = await _promotionStorage
                .GetCustomerPromotionRedemptionCountsAsync(req.SiteId, customerId, promoIds, cancelToken)
                .ConfigureAwait(false);
        }

        response.Data = PromotionEvaluator.Evaluate(candidates, req, defaults, utcNow, priorRedemptions);
        return response;
    }

    /// <summary>WooCommerce Promeng plugin reports store redemptions for unified KPI dashboard.</summary>
    public async Task<IApiResponse<object>> RecordExternalRedemptionsAsync(
        int siteId,
        RecordPromotionRedemptionsReq req,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<object> { Data = new { recorded = 0 } };
        if (req?.Redemptions == null || req.Redemptions.Count == 0)
            return response;

        var rows = new List<(int PromotionId, decimal DiscountAmount, DateTime RedeemedAtUtc, string Channel)>();
        foreach (var r in req.Redemptions)
        {
            if (!PromotionPromengWireMapper.TryParseExternalId(r.PromotionExternalId, out var promoId))
                continue;
            rows.Add((
                promoId,
                Math.Max(0m, r.DiscountAmount),
                ParseRedemptionTimestamp(r.RedeemedAt),
                string.IsNullOrWhiteSpace(r.Channel) ? PromotionWire.Channel.Web : r.Channel.Trim()));
        }

        if (rows.Count == 0)
            return response;

        var recorded = await _promotionStorage
            .RecordExternalRedemptionsAsync(siteId, rows, cancelToken)
            .ConfigureAwait(false);
        response.Data = new { recorded };
        return response;
    }

    private static DateTime ParseRedemptionTimestamp(string? redeemedAt)
    {
        if (string.IsNullOrWhiteSpace(redeemedAt))
            return DateTime.UtcNow;
        if (DateTime.TryParse(
                redeemedAt.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var utc))
            return utc;
        return DateTime.UtcNow;
    }

    public async Task<IApiResponse<PromotionCatalogBadgesRes>> GetCatalogBadgesAsync(
        int siteId,
        string? channel,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<PromotionCatalogBadgesRes>
        {
            Data = new PromotionCatalogBadgesRes(),
        };
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidId);

        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var utcNow = DateTime.UtcNow;
        var candidates = await _promotionStorage
            .GetActivePromotionsForEvaluationAsync(siteId, utcNow, cancelToken)
            .ConfigureAwait(false);

        var rules = PromotionCatalogBadgeResolver.ResolveRules(candidates, channel, utcNow);
        response.Data!.Rules = rules.Select(r => new PromotionCatalogBadgeRuleRes
        {
            PromotionId = r.PromotionId,
            Label = r.Label,
            PromotionType = string.IsNullOrWhiteSpace(r.PromotionType) ? null : r.PromotionType,
            DiscountKind = r.DiscountKind,
            DiscountValue = r.DiscountValue,
            WholeCart = r.WholeCart,
            AllProducts = r.AllProducts,
            ProductIds = r.ProductIds.OrderBy(x => x).ToList(),
            CategoryIds = r.CategoryIds.OrderBy(x => x).ToList(),
            ExcludedProductIds = r.ExcludedProductIds.OrderBy(x => x).ToList(),
        }).ToList();

        var scope = PromotionCatalogBadgeResolver.Resolve(candidates, channel, utcNow);
        response.Data.ProductIds = scope.ProductIds.OrderBy(x => x).ToList();
        response.Data.CategoryIds = scope.CategoryIds.OrderBy(x => x).ToList();
        response.Data.AllProducts = scope.AllProducts;
        return response;
    }

    private async Task<int> ResolveCustomerIdForEvaluateAsync(EvaluatePromotionsReq req, CancellationToken cancelToken)
    {
        if (int.TryParse(req.CustomerId, out var customerId) && customerId > 0)
            return customerId;
        if (string.IsNullOrWhiteSpace(req.CustomerPhone))
            return 0;
        var customer = await _customerStorage
            .GetCustomerByPhoneAsync(req.SiteId, req.CustomerPhone.Trim(), cancelToken)
            .ConfigureAwait(false);
        return customer?.Id ?? 0;
    }

    private async Task EnrichCatalogDiscountFlagsAsync(
        List<EvaluateCartLine> cart,
        DateTime utcNow,
        CancellationToken cancelToken)
    {
        var productIds = new List<int>();
        foreach (var line in cart)
        {
            if (int.TryParse(line.ProductId, out var pid) && pid > 0)
                productIds.Add(pid);
        }
        if (productIds.Count == 0) return;

        var onSale = await _productStorage
            .GetActiveCatalogSaleProductIdsAsync(productIds.Distinct().ToList(), utcNow, cancelToken)
            .ConfigureAwait(false);
        if (onSale.Count == 0) return;

        foreach (var line in cart)
        {
            if (int.TryParse(line.ProductId, out var pid) && onSale.Contains(pid))
                line.IsCatalogDiscounted = true;
        }
    }

    private static string? NormalizeCouponCode(string? coupon)
    {
        if (string.IsNullOrWhiteSpace(coupon)) return null;
        return coupon.Trim().ToLowerInvariant();
    }
}
