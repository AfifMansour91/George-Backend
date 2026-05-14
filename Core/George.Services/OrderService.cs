using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QRCoder;

namespace George.Services
{
    public class OrderService : ServiceBase
    {
        private readonly OrderStorage _orderStorage;
        private readonly CustomerStorage _customerStorage;
        private readonly SiteStorage _siteStorage;
        private readonly AccountStorage _accountStorage;
        private readonly ProductStorage _productStorage;
        private readonly SmsProvider _smsProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly PrintJobService _printJobService;
        private readonly string? _publicAppBaseUrl;
        private static readonly Dictionary<string, string> VoucherSourceLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Website"] = "אתר",
            ["WooCommerce"] = "אתר",
            ["Kiosk"] = "קיוסק",
            ["Phone"] = "טלפוני",
        };
        private const string VoucherQrCaption = "פתיחת הזמנה";
        private const string VoucherDateLabel = "תאריך אספקה:";
        private const string VoucherTimeLabel = "שעה:";
        private const string VoucherShippingLabel = "משלוח";
        private const string VoucherItemsTitle = "מוצרים בהזמנה";
        private const string VoucherNotesLabel = "הערות:";
        private const string VoucherVatIncludedLabel = "כולל מע״מ";
        private const string VoucherBrandFooter = "StoreOS";

        public OrderService(
            ILogger<OrderService> logger,
            IMapper mapper,
            CacheManager cache,
            OrderStorage orderStorage,
            CustomerStorage customerStorage,
            SiteStorage siteStorage,
            AccountStorage accountStorage,
            ProductStorage productStorage,
            SmsProvider smsProvider,
            IServiceScopeFactory serviceScopeFactory,
            PrintJobService printJobService,
            IConfiguration configuration)
            : base(logger, mapper, cache)
        {
            _orderStorage = orderStorage;
            _customerStorage = customerStorage;
            _siteStorage = siteStorage;
            _accountStorage = accountStorage;
            _productStorage = productStorage;
            _smsProvider = smsProvider;
            _serviceScopeFactory = serviceScopeFactory;
            _printJobService = printJobService;
            _publicAppBaseUrl = ResolvePublicAppBaseUrl(configuration);
        }

        /// <summary>Catalog line: <see cref="CreateOrderItemReq.ProductId"/> &gt; 0. Generic (phone) line: title + price + qty without product.</summary>
        private static bool IsValidCreateOrderLineItem(CreateOrderItemReq i)
        {
            if (i.ProductId is > 0)
                return true;
            if (string.IsNullOrWhiteSpace(i.Title))
                return false;
            if (i.PricePerUnit is null || i.PricePerUnit <= 0m)
                return false;
            return i.Quantity > 0m;
        }

        public async Task<IApiResponse<ApiListResponse<OrderRes>>> GetOrdersAsync(
            ApiListReq<OrderFilter> request,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<ApiListResponse<OrderRes>>
            {
                Data = new ApiListResponse<OrderRes>()
            };

            var res = await _orderStorage.GetOrdersAsync(request.Filter, request, cancelToken);
            response.Data!.Items = res.Items.ConvertAll(o => _mapper.Map<OrderRes>(o));
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;
            return response;
        }

        public async Task<IApiResponse<OrderRes>> GetOrderAsync(int orderId, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            response.Data = _mapper.Map<OrderRes>(order);
            return response;
        }

        public async Task<IApiResponse<OrderRes>> CreateOrderAsync(CreateOrderReq req, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            if (req.SiteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            if (req.Items == null || req.Items.Count == 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "At least one order item is required.");
            if (req.Items.Any(i => !IsValidCreateOrderLineItem(i)))
                return CreateResponse(response, StatusCode.InvalidRequest, "Each line needs a catalog ProductId, or a generic line with Title, PricePerUnit and Quantity.");
            if (string.IsNullOrWhiteSpace(req.CustomerName))
                return CreateResponse(response, StatusCode.InvalidRequest, "CustomerName is required.");
            if (string.IsNullOrWhiteSpace(req.CustomerPhone))
                return CreateResponse(response, StatusCode.InvalidRequest, "CustomerPhone is required.");

            if (req.AccountId <= 0)
            {
                var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken).ConfigureAwait(false);
                if (site == null)
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found.");
                req.AccountId = site.AccountId;
            }

            // Ensure customer exists for this site (find by SiteId + phone, or create); then link order to that customer. Pass marketingSms so it is persisted on the customer. Persist full delivery address on Customer (structured + combined line).
            var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                req.SiteId,
                req.AccountId,
                req.CustomerPhone,
                req.CustomerName!,
                email: req.CustomerEmail,
                city: req.DeliveryCity,
                defaultAddress: BuildCustomerDefaultDeliveryLine(req),
                notes: null,
                marketingSms: req.MarketingSms,
                deliveryStreet: req.DeliveryStreet,
                deliveryApartment: req.DeliveryApartment,
                deliveryFloor: req.DeliveryFloor,
                deliveryEntranceCode: req.DeliveryEntranceCode,
                cancelToken).ConfigureAwait(false);

            var order = _mapper.Map<Order>(req);
            RebuildDeliveryAddressFromStreetAndCity(order);
            order.CustomerId = customer.Id; // always set: customer was either found or created above
            order.CreationTime = DateTime.UtcNow;
            order.CreationUserId = AuthUser.Id;
            order.IsDeleted = false;
            NormalizeOrderDeliveryAndPickupDates(order);
            var items = new List<OrderItem>();
            var productCache = new Dictionary<int, Product?>();
            for (var i = 0; i < req.Items.Count; i++)
            {
                var lineReq = req.Items[i];
                var oi = _mapper.Map<OrderItem>(lineReq);
                oi.SortOrder = i;
                if (lineReq.ProductId is > 0)
                {
                    if (!productCache.TryGetValue(lineReq.ProductId.Value, out var product))
                    {
                        product = await _productStorage.GetProductAsync(lineReq.ProductId.Value, cancelToken).ConfigureAwait(false);
                        productCache[lineReq.ProductId.Value] = product;
                    }
                    OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, lineReq, product);
                }
                items.Add(oi);
            }
            var created = await _orderStorage.CreateOrderAsync(order, items, cancelToken);
            var loaded = await _orderStorage.GetOrderByIdAsync(created.Id, cancelToken);
            await TryApplyCompletionInventoryWhenOrderCompletedAsync(created.Id, previousStatus: null, loaded, cancelToken).ConfigureAwait(false);
            await TryApplyInternalOrderCatalogOnCreateAsync(loaded!, cancelToken).ConfigureAwait(false);
            var loadedAfterStock = await _orderStorage.GetOrderByIdAsync(created.Id, cancelToken).ConfigureAwait(false) ?? loaded;
            await TrySendNewOrderCustomerSmsAsync(loadedAfterStock!, cancelToken).ConfigureAwait(false);
            if (loadedAfterStock != null)
                await TryEnqueueNewOrderAutoPrintAsync(loadedAfterStock, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loadedAfterStock);
            return response;
        }

        /// <summary>Send customer SMS for new orders (Kiosk, Phone, Website, WooCommerce, …) using notification settings. Does not fail the request on SMS errors.</summary>
        /// <remarks>WooCommerce uses the same branch as Website: account notification "customer channel" must be SMS, then Pickup vs Shipping templates by order DeliveryType.</remarks>
        private async Task TrySendNewOrderCustomerSmsAsync(Order order, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(order.CustomerPhone))
                return;
            var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken).ConfigureAwait(false);
            var settings = account?.AccountNotificationSettings;
            if (settings == null)
                return;

            string? template = null;
            var source = (order.Source ?? "").Trim();
            if (string.Equals(source, "Kiosk", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(settings.NewOrderCustomerChannel, "sms", StringComparison.OrdinalIgnoreCase))
                    return;
                template = settings.NewOrderCustomerMessageKiosk;
            }
            else if (string.Equals(source, "Phone", StringComparison.OrdinalIgnoreCase))
            {
                if (!settings.NewOrderCustomerSmsOnPhoneOrderEnabled)
                    return;
                template = settings.NewOrderCustomerMessagePhoneOrder;
            }
            else
            {
                // Website/other sources: honor customer channel + delivery type templates.
                if (!string.Equals(settings.NewOrderCustomerChannel, "sms", StringComparison.OrdinalIgnoreCase))
                    return;
                var deliveryType = (order.DeliveryType ?? "").Trim();
                if (string.Equals(deliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                    template = settings.NewOrderCustomerMessageShipping;
                else if (string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase))
                    template = settings.NewOrderCustomerMessagePickup;
                else
                    template = settings.NewOrderCustomerMessagePickup ?? settings.NewOrderCustomerMessageShipping;
            }

            if (string.IsNullOrWhiteSpace(template))
                return;

            var body = ReplaceOrderPlaceholders(template, order);
            try
            {
                if (!SmsProvider.IsInitialized)
                {
                    _logger.LogWarning("SMS provider not initialized; skipping new-order customer SMS.");
                    return;
                }
                var sent = await _smsProvider.SendTextAsync(order.CustomerPhone, body, cancelToken).ConfigureAwait(false);
                if (!sent)
                    _logger.LogWarning("New-order customer SMS returned false for order {OrderId}.", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new-order customer SMS for order {OrderId}; order creation succeeded.", order.Id);
            }
        }

        /// <summary>Auto-send customer SMS when order transitions to Ready, according to OrderReady notification settings.</summary>
        private async Task TrySendOrderReadyCustomerSmsAsync(Order order, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(order.CustomerPhone))
                return;
            var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken).ConfigureAwait(false);
            var settings = account?.AccountNotificationSettings;
            if (settings == null)
                return;
            if (!string.Equals(settings.OrderReadyCustomerChannel, "sms", StringComparison.OrdinalIgnoreCase))
                return;

            // Kiosk orders use DeliveryType Pickup; must branch on Source before Pickup (same idea as new-order SMS).
            var template = ResolveOrderReadyCustomerMessageTemplate(settings, order);

            if (string.IsNullOrWhiteSpace(template))
                return;

            var body = ReplaceOrderPlaceholders(template, order);
            try
            {
                if (!SmsProvider.IsInitialized)
                {
                    _logger.LogWarning("SMS provider not initialized; skipping ready-order customer SMS.");
                    return;
                }
                var sent = await _smsProvider.SendTextAsync(order.CustomerPhone, body, cancelToken).ConfigureAwait(false);
                if (!sent)
                    _logger.LogWarning("Ready-order customer SMS returned false for order {OrderId}.", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ready-order customer SMS for order {OrderId}; order update succeeded.", order.Id);
            }
        }

        /// <summary>
        /// Order-ready SMS templates: kiosk orders are always Pickup; pick kiosk text when Source is Kiosk before Pickup branch.
        /// </summary>
        private static string? ResolveOrderReadyCustomerMessageTemplate(AccountNotificationSettings settings, Order order)
        {
            var deliveryType = (order.DeliveryType ?? "").Trim();
            var source = (order.Source ?? "").Trim();
            if (string.Equals(source, "Kiosk", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(settings.OrderReadyCustomerMessageKiosk))
                return settings.OrderReadyCustomerMessageKiosk;
            if (string.Equals(deliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                return settings.OrderReadyCustomerMessageShipping;
            if (string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase))
                return settings.OrderReadyCustomerMessagePickup;
            return settings.OrderReadyCustomerMessagePickup ?? settings.OrderReadyCustomerMessageShipping;
        }

        /// <summary>
        /// Match picking: if any line has picked qty &gt; 0, sum only those lines (+ shipping). Otherwise pre-pick — sum all line totals (new order SMS).
        /// </summary>
        private static decimal ResolveOrderTotalForPlaceholders(Order order)
        {
            var items = order.OrderItem;
            if (items == null || items.Count == 0)
                return order.Total ?? 0m;

            var anyPicked = items.Any(i => i.PickedQuantity.HasValue && i.PickedQuantity.Value > 0m);
            var shipping = order.ShippingCost ?? 0m;
            if (anyPicked)
            {
                var pickedSum = items.Sum(i =>
                {
                    if (!i.PickedQuantity.HasValue || i.PickedQuantity.Value <= 0m)
                        return 0m;
                    if (i.TotalPrice.HasValue)
                        return i.TotalPrice.Value;
                    return i.PickedQuantity.Value * (i.PricePerUnit ?? 0m);
                });
                return pickedSum + shipping;
            }

            var allLines = items.Sum(i => i.TotalPrice ?? i.Quantity * (i.PricePerUnit ?? 0m));
            if (allLines + shipping > 0m)
                return allLines + shipping;
            return order.Total ?? 0m;
        }

        private static string ReplaceOrderPlaceholders(string template, Order order)
        {
            var orderDate = order.CreationTime;
            var deliveryDate = order.DeliveryDate;
            var pickupDate = order.PickupDate;
            var orderTotalStr = ResolveOrderTotalForPlaceholders(order).ToString("N2", CultureInfo.InvariantCulture);
            return template
                .Replace("[customer_name]", order.CustomerName ?? "")
                .Replace("[order_number]", order.OrderNumber ?? "")
                .Replace("[order_date]", orderDate.ToString("dd/MM/yyyy"))
                .Replace("[order_total]", orderTotalStr)
                .Replace("[delivery_date]", deliveryDate.HasValue ? deliveryDate.Value.ToString("dd/MM/yyyy") : "")
                .Replace("[delivery_time]", order.DeliveryTime ?? "")
                .Replace("[pickup_date]", pickupDate.HasValue ? pickupDate.Value.ToString("dd/MM/yyyy") : "")
                .Replace("[pickup_time]", order.PickupTime ?? "");
        }

        /// <summary>Send reminder SMS to customer for a ready order (שלח תזכורת). Uses OrderReady message template from account notification settings.</summary>
        public async Task<IApiResponse<bool>> SendReminderAsync(int orderId, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<bool> { Data = false };
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            if (string.IsNullOrWhiteSpace(order.CustomerPhone))
                return CreateResponse(response, StatusCode.InvalidRequest, "Order has no customer phone.");
            var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken).ConfigureAwait(false);
            var settings = account?.AccountNotificationSettings;
            if (settings == null)
                return CreateResponse(response, StatusCode.InvalidRequest, "Notification settings not found.");
            if (!string.Equals(settings.OrderReadyCustomerChannel, "sms", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "Order ready customer channel is not SMS.");
            var template = ResolveOrderReadyCustomerMessageTemplate(settings, order);
            if (string.IsNullOrWhiteSpace(template))
                return CreateResponse(response, StatusCode.InvalidRequest, "No reminder message template configured for this order.");
            var body = ReplaceOrderPlaceholders(template, order);
            try
            {
                if (!SmsProvider.IsInitialized)
                {
                    _logger.LogWarning("SMS provider not initialized; cannot send reminder.");
                    return CreateResponse(response, StatusCode.InvalidRequest, "SMS provider not initialized.");
                }
                var sent = await _smsProvider.SendTextAsync(order.CustomerPhone, body, cancelToken).ConfigureAwait(false);
                response.Data = sent;
                if (!sent)
                    _logger.LogWarning("Send reminder SMS returned false for order {OrderId}.", orderId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder SMS for order {OrderId}.", orderId);
                return CreateResponse(response, StatusCode.GeneralError, ex.Message);
            }
        }

        public async Task<IApiResponse<OrderRes>> UpdateOrderAsync(int orderId, UpdateOrderReq req, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            var beforeUpdate = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            var previousStatus = beforeUpdate?.Status;
            var updated = await _orderStorage.UpdateOrderAsync(orderId, o =>
            {
                if (req.Status != null) o.Status = req.Status;
                if (req.ManagerNote != null) o.ManagerNote = req.ManagerNote;
                if (req.CustomerNote != null) o.CustomerNote = req.CustomerNote;
                if (req.DeliveryNote != null) o.DeliveryNote = req.DeliveryNote;
                if (req.DeliveryDate.HasValue) o.DeliveryDate = req.DeliveryDate.Value.ToDateTime(TimeOnly.MinValue);
                if (req.DeliveryTime != null) o.DeliveryTime = req.DeliveryTime;
                if (req.PickupDate.HasValue) o.PickupDate = req.PickupDate.Value.ToDateTime(TimeOnly.MinValue);
                if (req.PickupTime != null) o.PickupTime = req.PickupTime;
                if (req.DeliveryAddress != null) o.DeliveryAddress = req.DeliveryAddress;
                if (req.DeliveryType != null) o.DeliveryType = req.DeliveryType;
                var touchStreetOrCity = false;
                if (req.DeliveryStreet != null)
                {
                    o.DeliveryStreet = req.DeliveryStreet;
                    touchStreetOrCity = true;
                }
                if (req.DeliveryCity != null)
                {
                    o.DeliveryCity = req.DeliveryCity;
                    touchStreetOrCity = true;
                }
                if (req.DeliveryApartment != null) o.DeliveryApartment = req.DeliveryApartment;
                if (req.DeliveryFloor != null) o.DeliveryFloor = req.DeliveryFloor;
                if (req.DeliveryEntranceCode != null) o.DeliveryEntranceCode = req.DeliveryEntranceCode;
                if (req.PaymentStatus != null) o.PaymentStatus = req.PaymentStatus;
                if (req.PaymentMethod != null) o.PaymentMethod = req.PaymentMethod;
                if (req.BagsCount.HasValue) o.BagsCount = req.BagsCount;
                if (req.ShippingCost.HasValue) o.ShippingCost = req.ShippingCost.Value;
                if (req.SubTotal.HasValue) o.SubTotal = req.SubTotal.Value;
                if (req.Total.HasValue) o.Total = req.Total.Value;
                if (touchStreetOrCity)
                    RebuildDeliveryAddressFromStreetAndCity(o, clearCombinedLineWhenBothEmpty: true);
                o.UpdateUserId = AuthUser.Id;
            }, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, updated, "order update", statusOverrideForWcRest: null, cancelToken).ConfigureAwait(false);
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
            if (loaded != null && loaded.CustomerId is int customerId && customerId > 0)
            {
                var touchDelivery = req.DeliveryStreet != null || req.DeliveryCity != null || req.DeliveryApartment != null ||
                    req.DeliveryFloor != null || req.DeliveryEntranceCode != null || req.DeliveryAddress != null;
                if (touchDelivery)
                {
                    await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                        loaded.SiteId,
                        loaded.AccountId,
                        loaded.CustomerPhone,
                        loaded.CustomerName ?? "",
                        email: loaded.CustomerEmail,
                        city: loaded.DeliveryCity,
                        defaultAddress: JoinMainDeliveryLine(loaded.DeliveryStreet, loaded.DeliveryCity) ?? NullIfWhiteSpace(loaded.DeliveryAddress),
                        notes: null,
                        marketingSms: null,
                        deliveryStreet: loaded.DeliveryStreet,
                        deliveryApartment: loaded.DeliveryApartment,
                        deliveryFloor: loaded.DeliveryFloor,
                        deliveryEntranceCode: loaded.DeliveryEntranceCode,
                        cancelToken).ConfigureAwait(false);
                }
            }
            await TryApplyCompletionInventoryWhenOrderCompletedAsync(orderId, previousStatus, loaded, cancelToken).ConfigureAwait(false);
            if (loaded != null &&
                !string.Equals(previousStatus, "Ready", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loaded.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                await TrySendOrderReadyCustomerSmsAsync(loaded, cancelToken).ConfigureAwait(false);
            }
            response.Data = _mapper.Map<OrderRes>(loaded);
            return response;
        }

        public async Task<IApiResponse<OrderRes?>> CancelOrderAsync(int orderId, bool softDelete = true, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes?>();
            var beforeCancel = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (beforeCancel == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            if (!string.Equals(beforeCancel.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                var restoredProductIds = await TryRestoreCatalogStockOnOrderCancelAsync(beforeCancel, cancelToken).ConfigureAwait(false);
                if (restoredProductIds.Count > 0)
                    await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                        beforeCancel.SiteId,
                        restoredProductIds,
                        "order cancel (catalog restored)",
                        cancelToken).ConfigureAwait(false);
            }
            var order = await _orderStorage.CancelOrderAsync(orderId, AuthUser.Id, softDelete, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, order, "order cancel", statusOverrideForWcRest: "cancelled", cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(order);
            return response;
        }

        /// <summary>Get customer profile by phone at site (for manual order: name, manager note, stats).</summary>
        public async Task<IApiResponse<CustomerProfileRes>> GetCustomerProfileByPhoneAsync(int siteId, string? phone, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<CustomerProfileRes> { Data = new CustomerProfileRes() };
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            var profile = await _orderStorage.GetCustomerProfileByPhoneAsync(siteId, phone, cancelToken).ConfigureAwait(false);
            response.Data.Found = profile.Found;
            response.Data.CustomerName = profile.CustomerName;
            response.Data.CustomerPhone = profile.CustomerPhone;
            response.Data.ManagerNote = profile.ManagerNote;
            response.Data.LastOrderDate = profile.LastOrderDate;
            response.Data.OrderCount = profile.OrderCount;
            response.Data.AverageOrderTotal = profile.AverageOrderTotal;
            response.Data.TotalTransactions = profile.TotalTransactions;
            return response;
        }

        /// <summary>Get last order items by customer phone at site (for "last purchase" quick add to cart).</summary>
        public async Task<IApiResponse<List<OrderItemRes>>> GetLastOrderItemsByPhoneAsync(int siteId, string? phone, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<List<OrderItemRes>> { Data = new List<OrderItemRes>() };
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
            var order = await _orderStorage.GetLastOrderByCustomerPhoneAsync(siteId, phone, cancelToken).ConfigureAwait(false);
            if (order?.OrderItem != null)
                response.Data = order.OrderItem.Select(i => _mapper.Map<OrderItemRes>(i)).ToList();
            return response;
        }

        /// <summary>Add items to an existing order (picking "הוסף פריט"). Body: { "items": [ CreateOrderItemReq, ... ] }. Returns updated order.</summary>
        public async Task<IApiResponse<OrderRes>> AddItemsAsync(int orderId, List<CreateOrderItemReq>? items, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            if (items == null || items.Count == 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "At least one order item is required.");
            if (items.Any(i => !IsValidCreateOrderLineItem(i)))
                return CreateResponse(response, StatusCode.InvalidRequest, "Each line needs a catalog ProductId, or a generic line with Title, PricePerUnit and Quantity.");

            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "Cannot add items to a cancelled order.");

            var productCache = new Dictionary<int, Product?>();
            var newOrderItems = new List<OrderItem>();
            foreach (var lineReq in items)
            {
                var oi = _mapper.Map<OrderItem>(lineReq);
                if (lineReq.ProductId is > 0)
                {
                    if (!productCache.TryGetValue(lineReq.ProductId.Value, out var product))
                    {
                        product = await _productStorage.GetProductAsync(lineReq.ProductId.Value, cancelToken).ConfigureAwait(false);
                        productCache[lineReq.ProductId.Value] = product;
                    }
                    OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, lineReq, product);
                }
                newOrderItems.Add(oi);
            }
            var updated = await _orderStorage.AddOrderItemsAsync(orderId, newOrderItems, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var stockPushProductIds = new List<int>();
            var newLineIdsForBaseline = new List<int>();
            foreach (var oi in newOrderItems)
            {
                if (oi.ProductId is not > 0 || oi.Quantity <= 0m) continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        oi.ProductId.Value,
                        oi.ProductVariantId,
                        oi.Quantity,
                        cancelToken)
                    .ConfigureAwait(false);
                stockPushProductIds.Add(oi.ProductId.Value);
                if (oi.Id > 0)
                    newLineIdsForBaseline.Add(oi.Id);
            }
            if (newLineIdsForBaseline.Count > 0)
                await _orderStorage
                    .SetPickedQuantityBaselineForOrderItemIdsAsync(orderId, newLineIdsForBaseline, cancelToken)
                    .ConfigureAwait(false);

            if (stockPushProductIds.Count > 0)
                await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                    updated.SiteId,
                    stockPushProductIds,
                    "add order items",
                    cancelToken).ConfigureAwait(false);

            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
            if (loaded != null)
                await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, loaded, "add items", statusOverrideForWcRest: null, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loaded);
            return response;
        }

        /// <summary>Remove a single item from an order (picking "הסר מוצר"). Returns updated order.</summary>
        public async Task<IApiResponse<OrderRes>> RemoveOrderItemAsync(int orderId, int orderItemId, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "Cannot remove items from a cancelled order.");
            var line = order.OrderItem?.FirstOrDefault(i => i.Id == orderItemId && !i.IsDeleted);
            var restoreProductId = line?.ProductId;
            var restoreVariantId = line?.ProductVariantId;
            var restorePicked = line?.PickedQuantity;

            var updated = await _orderStorage.RemoveOrderItemAsync(orderId, orderItemId, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order item not found.");

            if (restoreProductId is > 0 && restorePicked is > 0m)
            {
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        restoreProductId.Value,
                        restoreVariantId,
                        -restorePicked.Value,
                        cancelToken)
                    .ConfigureAwait(false);
                await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                    updated.SiteId,
                    new List<int> { restoreProductId.Value },
                    "remove item (stock restored)",
                    cancelToken).ConfigureAwait(false);
            }

            await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, updated, "remove item", statusOverrideForWcRest: null, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(updated);
            return response;
        }

        /// <summary>Save picking state (שמור וצא). Body: { "items": [ { "orderItemId", "pickedQuantity", "totalPrice" }, ... ] }.</summary>
        public async Task<IApiResponse<OrderRes>> UpdatePickingAsync(int orderId, UpdatePickingReq? req, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            if (req?.Items == null || req.Items.Count == 0)
            {
                var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
                if (order == null) return CreateResponse(response, StatusCode.ItemNotFound);
                response.Data = _mapper.Map<OrderRes>(order);
                return response;
            }
            var orderCheck = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (orderCheck == null) return CreateResponse(response, StatusCode.ItemNotFound);
            if (string.Equals(orderCheck.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "Cannot update picking for a cancelled order.");
            var updates = req.Items
                .Where(i => i.OrderItemId > 0)
                .Select(i => (i.OrderItemId, i.PickedQuantity, i.TotalPrice))
                .ToList();
            var updated = await _orderStorage.UpdatePickingAsync(orderId, updates, cancelToken);
            if (updated == null) return CreateResponse(response, StatusCode.ItemNotFound);

            var stockPushProductIds = new List<int>();
            foreach (var (orderItemId, newPicked, _) in updates)
            {
                var line = orderCheck.OrderItem?.FirstOrDefault(i => i.Id == orderItemId && !i.IsDeleted);
                if (line == null || line.ProductId is not > 0) continue;
                var oldPicked = line.PickedQuantity ?? 0m;
                var newPickedVal = newPicked ?? 0m;
                var consumptionDelta = newPickedVal - oldPicked;
                if (consumptionDelta == 0m) continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        line.ProductId.Value,
                        line.ProductVariantId,
                        consumptionDelta,
                        cancelToken)
                    .ConfigureAwait(false);
                stockPushProductIds.Add(line.ProductId.Value);
            }
            if (stockPushProductIds.Count > 0)
                await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                    orderCheck.SiteId,
                    stockPushProductIds,
                    "picking update",
                    cancelToken).ConfigureAwait(false);
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
            var forResponse = loaded ?? updated;
            if (forResponse != null)
                await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, forResponse, "picking update", statusOverrideForWcRest: null, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(forResponse);
            return response;
        }

        /// <summary>After order mutations that should mirror to WooCommerce/oc-storeos (full POST for oc-storeos). Uses a new DI scope inside background work to avoid DbContext concurrency.</summary>
        private async Task ScheduleWooCommerceStoreSyncIfApplicableAsync(
            int orderId,
            Order order,
            string logReason,
            string? statusOverrideForWcRest,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(order.ExternalOrderId) ||
                !string.Equals(order.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase))
                return;
            var site = await _siteStorage.GetSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);
            if (site?.WooCommerceEnabled != true)
                return;
            var wcStatus = statusOverrideForWcRest ?? MapOurStatusToWooCommerce(order.Status) ?? "on-hold";
            var siteIdCapture = order.SiteId;
            var externalIdCapture = order.ExternalOrderId!;
            var orderIdCapture = orderId;
            _logger.LogInformation(
                "WooCommerce store sync scheduled ({Reason}). internalOrderId={InternalOrderId}, siteId={SiteId}, externalStoreOrderId={ExternalStoreOrderId}, mappedStoreStatus={MappedStoreStatus}",
                logReason, orderIdCapture, siteIdCapture, externalIdCapture, wcStatus);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                    await wooCommerceService.UpdateOrderStatusAsync(siteIdCapture, externalIdCapture, wcStatus, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WooCommerce/oc-storeos order sync failed for order {OrderId}", orderIdCapture);
                }
            }, CancellationToken.None);
        }

        /// <summary>
        /// Pushes Store catalog stock for the given products to WooCommerce (REST product sync). Best-effort background work.
        /// Use after picking / completion / line removal so Woo reflects the same quantities as StoreOS.
        /// </summary>
        private async Task ScheduleWooCommerceCatalogStockPushForProductsAsync(
            int siteId,
            IReadOnlyList<int> productIds,
            string logReason,
            CancellationToken cancelToken)
        {
            var ids = productIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return;
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            if (site?.WooCommerceEnabled != true) return;
            var siteIdCapture = siteId;
            var idsCapture = ids;
            _logger.LogInformation(
                "WooCommerce catalog stock push scheduled ({Reason}). siteId={SiteId}, productCount={Count}",
                logReason, siteIdCapture, idsCapture.Count);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                    var req = new WooCommerceSyncReq { SiteId = siteIdCapture, ProductIds = idsCapture };
                    await wooCommerceService.SyncToWooCommerceAsync(req, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "WooCommerce catalog stock push failed ({Reason}). siteId={SiteId}",
                        logReason,
                        siteIdCapture);
                }
            }, CancellationToken.None);
        }

        /// <summary>Parse shippingInfo and shipping_label into delivery type, date, time and optional note. Date may be "DD/MM/YYYY" or "YYYY-MM-DD".</summary>
        private static void ApplyShippingInfoToOrder(Order order, WooCommerceShippingInfoPayload? shippingInfo, string? shippingLabel)
        {
            var isPickup = false;
            if (shippingInfo?.Type != null)
            {
                var t = shippingInfo.Type.Trim();
                isPickup = string.Equals(t, "pickup", StringComparison.OrdinalIgnoreCase);
                order.DeliveryType = isPickup ? "Pickup" : "Shipping";
            }
            else if (!string.IsNullOrWhiteSpace(shippingLabel))
            {
                var label = shippingLabel.Trim();
                isPickup = label.Contains("איסוף", StringComparison.Ordinal) || label.Contains("pickup", StringComparison.OrdinalIgnoreCase);
                order.DeliveryType = isPickup ? "Pickup" : "Shipping";
            }
            if (shippingInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(shippingInfo.Date))
                {
                    var parsed = ParseWooCommerceDate(shippingInfo.Date);
                    if (parsed.HasValue)
                    {
                        var cal = DateTime.SpecifyKind(parsed.Value.Date, DateTimeKind.Unspecified);
                        if (isPickup) order.PickupDate = cal;
                        else order.DeliveryDate = cal;
                    }
                }
                var slot = string.IsNullOrWhiteSpace(shippingInfo.SlotStart) ? shippingInfo.SlotEnd : shippingInfo.SlotStart;
                if (!string.IsNullOrWhiteSpace(shippingInfo.SlotEnd) && shippingInfo.SlotEnd != slot)
                    slot = $"{slot?.Trim()} - {shippingInfo.SlotEnd.Trim()}";
                if (!string.IsNullOrWhiteSpace(slot))
                {
                    if (isPickup) order.PickupTime = slot.Trim();
                    else order.DeliveryTime = slot.Trim();
                }
                if (!string.IsNullOrWhiteSpace(shippingInfo.PickupAffiliateName))
                    order.DeliveryNote = shippingInfo.PickupAffiliateName.Trim();
                else if (!string.IsNullOrWhiteSpace(shippingInfo.PickupAffiliateId))
                    order.DeliveryNote = shippingInfo.PickupAffiliateId.Trim();
            }
        }

        /// <summary>
        /// <see cref="ApplyShippingInfoToOrder"/> sets only <see cref="Order.PickupDate"/> for pickup or only <see cref="Order.DeliveryDate"/> for shipping.
        /// Copy the scheduled date to the sibling field before falling back to today so pickup orders do not keep <see cref="Order.DeliveryDate"/> empty and then get forced to "today".
        /// </summary>
        /// <remarks>
        /// "Today" for the fallback uses Israel calendar date (Asia/Jerusalem), not UTC midnight, so late-night local orders
        /// still match the shop-manager "delivery today" filter which uses the manager's local calendar date.
        /// </remarks>
        private static void NormalizeOrderDeliveryAndPickupDates(Order order)
        {
            var pickup = ToUnspecifiedCalendarDate(order.PickupDate);
            var delivery = ToUnspecifiedCalendarDate(order.DeliveryDate);
            var todayCal = GetIsraelCalendarTodayUnspecified();

            if (!delivery.HasValue)
                delivery = pickup ?? todayCal;
            if (!pickup.HasValue)
                pickup = delivery ?? todayCal;

            order.PickupDate = pickup;
            order.DeliveryDate = delivery;
        }

        /// <summary>Calendar date (date-only, Unspecified kind) in Israel — aligns default delivery/pickup with Hebrew storefront day boundary.</summary>
        private static DateTime GetIsraelCalendarTodayUnspecified()
        {
            try
            {
                var tzId = OperatingSystem.IsWindows() ? "Israel Standard Time" : "Asia/Jerusalem";
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                var israelNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return DateTime.SpecifyKind(israelNow.Date, DateTimeKind.Unspecified);
            }
            catch
            {
                return DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
            }
        }

        private static DateTime? ToUnspecifiedCalendarDate(DateTime? value) =>
            value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified) : null;

        private static DateTime? ParseWooCommerceDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            var s = dateStr.Trim();
            var parts = s.Split('/', '-', '.');
            if (parts.Length == 3 && int.TryParse(parts[0], out var n1) && int.TryParse(parts[1], out var n2) && int.TryParse(parts[2], out var y) && y >= 2000 && y <= 2100)
            {
                int day, month;
                if (n1 > 12 && n2 >= 1 && n2 <= 12)
                {
                    day = n1;
                    month = n2;
                }
                else if (n2 > 12 && n1 >= 1 && n1 <= 12)
                {
                    day = n2;
                    month = n1;
                }
                else if (n1 >= 1 && n1 <= 31 && n2 >= 1 && n2 <= 12)
                {
                    day = n1;
                    month = n2;
                }
                else
                {
                    if (DateTime.TryParse(s, out var dt)) return dt;
                    return null;
                }
                if (day >= 1 && day <= DateTime.DaysInMonth(y, month))
                    return new DateTime(y, month, day);
            }
            if (DateTime.TryParse(s, out var parsed)) return parsed;
            return null;
        }

        /// <summary>Parse plugin <c>saleTotalWeight</c>: plain kg number, <c>1.5 ק"ג</c>, or grams e.g. <c>600 גר'</c>.</summary>
        private static decimal? TryParseSaleTotalWeightKg(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim().Replace(',', '.');
            if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0)
                return d;
            var m = Regex.Match(t, @"^\s*(\d+(?:\.\d+)?)");
            if (!m.Success) return null;
            if (!decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) || n <= 0)
                return null;
            if (t.Contains("קג", StringComparison.Ordinal) || t.Contains("ק\"ג", StringComparison.Ordinal))
                return n;
            if (t.Contains("גר", StringComparison.Ordinal))
                return n / 1000m;
            return null;
        }

        /// <summary>Leading number in strings like "1 יח'" or "2.5 יחידות".</summary>
        private static decimal? TryParseSaleUnitsCount(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var m = Regex.Match(s.Trim(), @"^\s*(\d+(?:\.\d+)?)");
            if (!m.Success) return null;
            if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0)
                return d;
            return null;
        }

        private static string? NullIfWhiteSpace(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Main delivery line: רחוב, עיר, מיקוד (ללא דירה/קומה/קוד — נשמרים בעמודות נפרדות).</summary>
        private static string? JoinMainDeliveryLine(string? street, string? city, string? zip = null)
        {
            var parts = new List<string>();
            void AddPart(string? s)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add(s.Trim());
            }
            AddPart(street);
            AddPart(city);
            AddPart(zip);
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>אם נשלחו רחוב/עיר — מעדכן את <see cref="Order.DeliveryAddress"/> לשורה אחת. בעדכון, אם שניהם ריקים אחרי עריכה — מנקה את השורה המשולבת.</summary>
        private static void RebuildDeliveryAddressFromStreetAndCity(Order o, bool clearCombinedLineWhenBothEmpty = false)
        {
            var line = JoinMainDeliveryLine(o.DeliveryStreet, o.DeliveryCity);
            if (!string.IsNullOrWhiteSpace(line))
            {
                o.DeliveryAddress = line;
                return;
            }
            if (clearCombinedLineWhenBothEmpty &&
                string.IsNullOrWhiteSpace(o.DeliveryStreet) &&
                string.IsNullOrWhiteSpace(o.DeliveryCity))
                o.DeliveryAddress = null;
        }

        private static string? BuildCustomerDefaultDeliveryLine(CreateOrderReq req) =>
            JoinMainDeliveryLine(req.DeliveryStreet, req.DeliveryCity) ?? NullIfWhiteSpace(req.DeliveryAddress);

        /// <summary>Maps WooCommerce shipping JSON into structured columns + main address line (street, city, zip).</summary>
        private static void ApplyWooCommerceShippingAddressToOrder(Order o, WooCommerceShippingAddressPayload? a)
        {
            if (a == null) return;
            o.DeliveryStreet = NullIfWhiteSpace(a.Street);
            o.DeliveryCity = NullIfWhiteSpace(a.City);
            o.DeliveryApartment = NullIfWhiteSpace(a.Apartment);
            o.DeliveryFloor = NullIfWhiteSpace(a.Floor);
            o.DeliveryEntranceCode = NullIfWhiteSpace(a.ResolvedEntranceCode);
            o.DeliveryAddress = JoinMainDeliveryLine(a.Street, a.City, a.Zip);
        }

        /// <summary>Pickup branch name from plugin <c>shippingstorename</c> when delivery type is pickup.</summary>
        private static void ApplyWooCommercePickupStoreNote(Order order, string? shippingStoreName)
        {
            var name = NullIfWhiteSpace(shippingStoreName);
            if (name == null) return;
            if (!string.Equals(order.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(order.DeliveryNote))
            {
                order.DeliveryNote = name;
                return;
            }
            if (order.DeliveryNote.Contains(name, StringComparison.Ordinal)) return;
            order.DeliveryNote = $"{name} — {order.DeliveryNote}";
        }

        /// <summary>Map Woo payment code/title/label to internal payment method (e.g. Cash for cod or מזומן).</summary>
        private static string? MapWooCommercePaymentMethodToInternal(string? code, string? title, string? paymentLabel = null)
        {
            var pl = paymentLabel?.Trim();
            if (!string.IsNullOrWhiteSpace(pl))
            {
                if (pl.Contains("מזומן", StringComparison.Ordinal)) return "Cash";
                if (pl.Contains("אשראי", StringComparison.Ordinal)) return "CreditCard";
                // COD-style labels from Hebrew stores (e.g. "במסירה", "תשלום במסירה")
                if (pl.Contains("מסירה", StringComparison.Ordinal)) return "Cash";
            }
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(title)) return null;
            var c = (code ?? "").Trim().ToLowerInvariant();
            if (c == "cod") return "Cash";
            if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
            return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }

        /// <summary>
        /// Woo billing note → <see cref="Order.BillingNotes"/> (canonical) and <see cref="Order.CustomerNote"/> (shop UI).
        /// Woo customer checkout note is appended to <see cref="Order.CustomerNote"/> when both exist.
        /// </summary>
        private static (string? BillingNotes, string? CustomerNote) ResolveWooCommerceNotesForPersistence(WooCommerceOrderPayload p)
        {
            var bill = p.GetResolvedBillingNotes();
            var cust = string.IsNullOrWhiteSpace(p.CustomerNotes) ? null : p.CustomerNotes.Trim();
            string? customerNote;
            if (cust != null && bill != null)
                customerNote = $"{cust}\n\n{bill}";
            else
                customerNote = bill ?? cust;
            return (bill, customerNote);
        }

        /// <summary>Persist WooCommerce-only order fields (labels, billing/internal notes, site/affiliate echo).</summary>
        private static void ApplyWooCommerceStoredMetadata(Order o, WooCommerceOrderPayload p)
        {
            o.InternalOrderNotes = p.InternalOrderNotes;
            o.PaymentMethodTitle = p.PaymentMethodTitle;
            o.PaymentLabel = p.GetResolvedPaymentLabel();
            o.ShippingLabel = p.GetResolvedShippingLabel();
            o.WooCommerceSiteId = p.SiteId;
            o.WooCommercePickupAffiliateId = p.ShippingInfo?.PickupAffiliateId;
        }

        private static readonly JsonSerializerOptions WooCommerceOrderRequestJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>JSON snapshot of the deserialized payload for <see cref="Order.WooCommerceRequestJson"/> (audit; not byte-identical to raw HTTP).</summary>
        private static string? SerializeWooCommerceOrderPayloadForStorage(WooCommerceOrderPayload p)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(p, WooCommerceOrderRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static string? SerializeWooCommerceFragmentForStorage<T>(T? value)
        {
            if (value == null) return null;
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(value, WooCommerceOrderRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static string? SerializeWooCommerceOrderItemLineForStorage(WooCommerceOrderItemPayload it)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(it, WooCommerceOrderRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Persist WooCommerce payload fragments as first-class DB columns (in addition to <see cref="Order.WooCommerceRequestJson"/>).</summary>
        private static void ApplyWooCommercePayloadColumnSnapshot(Order o, WooCommerceOrderPayload p)
        {
            o.ExternalOrderStatusRaw = NullIfWhiteSpace(p.Status);
            o.GatewayPaymentMethodCode = NullIfWhiteSpace(p.PaymentMethod);
            o.ShippingStoreName = NullIfWhiteSpace(p.GetResolvedShippingStoreName());
            o.CouponCode = NullIfWhiteSpace(p.GetResolvedCouponCodeForStorage());
            o.ShippingInfoJson = SerializeWooCommerceFragmentForStorage(p.ShippingInfo);
            o.ShippingAddressJson = SerializeWooCommerceFragmentForStorage(p.ShippingAddress);
            o.OrderCustomerJson = SerializeWooCommerceFragmentForStorage(p.Customer);
        }

        private static void PopulateWooCommerceOrderItemPayloadColumns(OrderItem oi, WooCommerceOrderItemPayload it)
        {
            oi.LineSku = NullIfWhiteSpace(it.Sku);
            oi.LineQuantityType = NullIfWhiteSpace(it.QuantityType);
            oi.LineUnit = it.Unit;
            oi.LineUnitWeightKg = it.UnitWeight;
            oi.SaleUnitsLine = NullIfWhiteSpace(it.SaleUnitsLine);
            oi.LinePayloadJson = SerializeWooCommerceOrderItemLineForStorage(it);
        }

        /// <summary>
        /// Sets <see cref="OrderItem.OrderLineQuantityMode"/> from Woo <c>quantityType</c> so shop-manager badges match the storefront (kg total vs יח').
        /// Runs after <see cref="OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields"/> so it overrides wrong heuristics when <c>saleUnits</c>/<c>saleTotalWeight</c> are empty.
        /// When <c>quantityType=kg</c> but <c>saleUnits</c> indicates piece-based sale (e.g. "2 יח'"), the display unit is pieces — quantity was stored in kg by the plugin, but the item is sold by piece count.
        /// </summary>
        private static void ApplyWooCommerceQuantityTypeToLineDisplay(OrderItem oi, WooCommerceOrderItemPayload it)
        {
            if (string.IsNullOrWhiteSpace(it.QuantityType)) return;

            if (string.Equals(it.QuantityType, "kg", StringComparison.OrdinalIgnoreCase))
            {
                // When unit > 0 the plugin is telling us this is a piece-based sale (N pieces weighed in kg).
                // The display unit for the customer is pieces, not kg total.
                if (it.Unit is > 0)
                {
                    oi.OrderLineQuantityMode = "units";
                    return;
                }
                oi.OrderLineQuantityMode = "weight";
                // quantity is total kg; UI derives total grams as Quantity × UnitWeightGrams when saleTotalWeight is empty.
                if (oi.Quantity > 0m && (oi.UnitWeightGrams == null || oi.UnitWeightGrams <= 0m))
                    oi.UnitWeightGrams = 1000m;
                return;
            }

            if (string.Equals(it.QuantityType, "unit", StringComparison.OrdinalIgnoreCase))
                oi.OrderLineQuantityMode = "units";
        }

        /// <summary>Resolves WooCommerce order item to our Product.Id: parent <see cref="Product.WooCommerceId"/>, then variation <see cref="ProductVariant.WooCommerceVariationId"/> (WC often sends variation id as product_id), then SKU. Returns null if not found (caller may keep payload ProductId as fallback).</summary>
        private async Task<int?> ResolveWooCommerceItemProductIdAsync(int siteId, int accountId, int? wooCommerceProductId, string? sku, int? wooCommerceVariationId, CancellationToken cancelToken)
        {
            if (wooCommerceProductId.HasValue && wooCommerceProductId.Value > 0)
            {
                var byWooId = await _productStorage.GetProductIdByWooCommerceIdAndSiteAsync(siteId, wooCommerceProductId.Value, cancelToken).ConfigureAwait(false);
                if (byWooId.HasValue) return byWooId.Value;
                var byPidAsVariation = await _productStorage.GetProductIdByWooCommerceVariationIdAndSiteAsync(siteId, wooCommerceProductId.Value, cancelToken).ConfigureAwait(false);
                if (byPidAsVariation.HasValue) return byPidAsVariation.Value;
            }
            if (wooCommerceVariationId.HasValue && wooCommerceVariationId.Value > 0)
            {
                var byVar = await _productStorage.GetProductIdByWooCommerceVariationIdAndSiteAsync(siteId, wooCommerceVariationId.Value, cancelToken).ConfigureAwait(false);
                if (byVar.HasValue) return byVar.Value;
            }
            if (!string.IsNullOrWhiteSpace(sku))
            {
                var bySku = await _productStorage.GetProductBySkuAndSitesAsync(sku.Trim(), accountId, new List<int> { siteId }, false, cancelToken).ConfigureAwait(false);
                if (bySku != null) return bySku.Id;
            }
            return null;
        }

        /// <summary>Build VariantTitle from payload: variants[].name joined " | ", or variation.attributes/meta values joined " | " when plugin sends variation object.</summary>
        private static string? GetVariantTitleFromPayload(WooCommerceOrderItemPayload it)
        {
            if (it.Variants != null && it.Variants.Count > 0)
            {
                var names = it.Variants
                    .Select(v => v?.Name?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                if (names.Count > 0) return string.Join(" | ", names);
            }
            if (it.Variation?.Attributes != null && it.Variation.Attributes.Count > 0)
            {
                var values = it.Variation.Attributes
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                if (values.Count > 0) return string.Join(" | ", values);
            }
            if (it.Variation?.Meta != null && it.Variation.Meta.Count > 0)
            {
                var values = it.Variation.Meta
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                if (values.Count > 0) return string.Join(" | ", values);
            }
            return null;
        }

        private static int? GetEffectiveVariationId(WooCommerceOrderItemPayload it)
        {
            if (it.VariationId.HasValue && it.VariationId.Value > 0) return it.VariationId.Value;
            if (it.Variation?.VariationId.HasValue == true && it.Variation.VariationId!.Value > 0) return it.Variation.VariationId.Value;
            return null;
        }

        /// <summary>Resolve WooCommerce order item to our ProductVariant for this product (site product already resolved). First by variationId (WooCommerceVariationId), then by variant names joined with " | ". Used only when processing WooCommerce orders.</summary>
        private static ProductVariant? GetVariantFromPayloadItem(WooCommerceOrderItemPayload it, Product? product)
        {
            if (product?.ProductVariant == null || !product.ProductVariant.Any(v => !v.IsDeleted))
                return null;

            // 1) Match by WooCommerce variation ID when sent (item level or inside variation object)
            var effectiveVariationId = GetEffectiveVariationId(it);
            if (effectiveVariationId.HasValue)
            {
                var byId = product.ProductVariant
                    .FirstOrDefault(v => !v.IsDeleted && v.WooCommerceVariationId == effectiveVariationId.Value);
                if (byId != null) return byId;
            }

            // 2) Fallback: match by name (payload variants[].name joined " | " vs our variant option values joined " | ")
            var payloadTitle = GetVariantTitleFromPayload(it);
            if (string.IsNullOrWhiteSpace(payloadTitle)) return null;

            var payloadNorm = payloadTitle.Trim();
            foreach (var v in product.ProductVariant.Where(v => !v.IsDeleted))
            {
                var optionValues = (v.ProductVariantOptionValue?
                    .OrderBy(ov => ov.OptionName)
                    .Select(ov => ov.OptionValue?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? new List<string?>()).Cast<string>().ToList();
                var ourTitle = optionValues.Count > 0 ? string.Join(" | ", optionValues) : null;
                if (!string.IsNullOrWhiteSpace(ourTitle) && string.Equals(ourTitle.Trim(), payloadNorm, StringComparison.OrdinalIgnoreCase))
                    return v;
            }
            return null;
        }

        /// <summary>For WooCommerce order items: compute quantity, unitWeightGrams and optional variantTitle from product setup so display matches Kiosk/manual. Uses <see cref="WooCommerceOrderItemPayload.SaleUnits"/> / <see cref="WooCommerceOrderItemPayload.SaleTotalWeight"/> when sent (same intent as manual cart: units + total kg).</summary>
        private static (decimal quantity, decimal? unitWeightGrams, string? variantTitle) GetWooCommerceItemQuantityAndUnitWeight(WooCommerceOrderItemPayload it, Product? product)
        {
            var saleWeightKg = TryParseSaleTotalWeightKg(it.SaleTotalWeight);
            var saleUnits = TryParseSaleUnitsCount(it.SaleUnits);

            if (string.Equals(it.QuantityType, "kg", StringComparison.OrdinalIgnoreCase)
                && it.Unit is > 0
                && it.UnitWeight is > 0)
            {
                saleUnits = it.Unit;
                saleWeightKg = it.Unit!.Value * it.UnitWeight!.Value;
            }
            else if (string.Equals(it.QuantityType, "unit", StringComparison.OrdinalIgnoreCase)
                     && it.Unit is > 0
                     && !saleUnits.HasValue)
            {
                saleUnits = it.Unit;
            }

            if (product == null)
                return (saleUnits ?? it.Quantity, null, null);

            var setupTypeName = product.SetupType?.Name ?? "";
            var isWeightedBySetup = setupTypeName is "by_weight" or "by_unit" or "by_unit_and_weight";
            var isWeighted = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetup);

            if (!isWeighted || setupTypeName == "standard")
                return (saleUnits ?? it.Quantity, null, null);

            // Sold by weight (ק"ג or גרם): quantity stored as kg with unitWeightGrams=1000 (display = quantity × 1000 g).
            if (setupTypeName == "by_weight")
            {
                var weightKg = saleWeightKg ?? it.Quantity;
                return (weightKg, 1000m, null);
            }

            // by_unit or by_unit_and_weight
            var wc = product.WeightConfig;
            if (wc?.WeightByVariant == true)
            {
                decimal? unitWeightGrams = null;
                var matchedVariant = GetVariantFromPayloadItem(it, product);
                var variantToUse = matchedVariant ?? product.ProductVariant?.FirstOrDefault(v => !v.IsDeleted && v.Weight.HasValue);
                if (variantToUse?.Weight.HasValue == true)
                    unitWeightGrams = (decimal)(variantToUse.Weight!.Value * 1000);

                // Plugin sends explicit units + total kg (e.g. 1 יח' + 0.8 kg) — align with manual order (whole units + per-unit grams).
                if (saleUnits.HasValue && saleWeightKg.HasValue && saleUnits.Value > 0)
                {
                    var qty = decimal.Round(saleUnits.Value, 3, MidpointRounding.AwayFromZero);
                    var gramsPerUnitFromSale = saleWeightKg.Value * 1000m / saleUnits.Value;
                    if (unitWeightGrams.HasValue && unitWeightGrams.Value > 0)
                    {
                        var ratio = Math.Abs(gramsPerUnitFromSale - unitWeightGrams.Value) / unitWeightGrams.Value;
                        if (ratio <= 0.2m)
                            return (qty, unitWeightGrams, null);
                    }
                    return (qty, decimal.Round(gramsPerUnitFromSale, 3, MidpointRounding.AwayFromZero), null);
                }

                if (saleUnits.HasValue && saleUnits.Value > 0 && unitWeightGrams.HasValue)
                    return (decimal.Round(saleUnits.Value, 3, MidpointRounding.AwayFromZero), unitWeightGrams, null);

                if (saleWeightKg.HasValue && unitWeightGrams.HasValue && unitWeightGrams.Value > 0)
                {
                    var qty = decimal.Round(saleWeightKg.Value * 1000m / unitWeightGrams.Value, 3, MidpointRounding.AwayFromZero);
                    return (qty, unitWeightGrams, null);
                }

                var quantity = it.Quantity;
                var isFractionalQuantity = quantity != decimal.Truncate(quantity);
                if (isFractionalQuantity && unitWeightGrams.HasValue && unitWeightGrams.Value > 0)
                {
                    var orderedWeightGrams = string.Equals(wc.Unit?.Name, "g", StringComparison.OrdinalIgnoreCase)
                        ? quantity
                        : quantity * 1000m;
                    quantity = decimal.Round(orderedWeightGrams / unitWeightGrams.Value, 3, MidpointRounding.AwayFromZero);
                }
                return (quantity, unitWeightGrams, null);
            }

            if (!string.IsNullOrWhiteSpace(wc?.UnitWeight) && decimal.TryParse(wc.UnitWeight, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                var unitWeightGrams = string.Equals(wc.Unit?.Name, "g", StringComparison.OrdinalIgnoreCase)
                    ? parsed
                    : parsed * 1000m;

                if (saleUnits.HasValue && saleWeightKg.HasValue && saleUnits.Value > 0)
                {
                    var qty = decimal.Round(saleUnits.Value, 3, MidpointRounding.AwayFromZero);
                    var gramsPerUnitFromSale = saleWeightKg.Value * 1000m / saleUnits.Value;
                    if (unitWeightGrams > 0)
                    {
                        var ratio = Math.Abs(gramsPerUnitFromSale - unitWeightGrams) / unitWeightGrams;
                        if (ratio <= 0.2m)
                            return (qty, unitWeightGrams, null);
                    }
                    return (qty, decimal.Round(gramsPerUnitFromSale, 3, MidpointRounding.AwayFromZero), null);
                }

                if (saleUnits.HasValue && saleUnits.Value > 0)
                    return (decimal.Round(saleUnits.Value, 3, MidpointRounding.AwayFromZero), unitWeightGrams, null);

                if (saleWeightKg.HasValue && unitWeightGrams > 0)
                {
                    var qty = decimal.Round(saleWeightKg.Value * 1000m / unitWeightGrams, 3, MidpointRounding.AwayFromZero);
                    return (qty, unitWeightGrams, null);
                }

                var quantity = it.Quantity;
                var isFractionalQuantity = quantity != decimal.Truncate(quantity);
                if (isFractionalQuantity && unitWeightGrams > 0)
                {
                    var orderedWeightGrams = string.Equals(wc.Unit?.Name, "g", StringComparison.OrdinalIgnoreCase)
                        ? quantity
                        : quantity * 1000m;
                    quantity = decimal.Round(orderedWeightGrams / unitWeightGrams, 3, MidpointRounding.AwayFromZero);
                }
                return (quantity, unitWeightGrams, null);
            }

            // by_unit_and_weight with no unit weight: treat WC quantity / saleTotalWeight as weight in kg.
            if (setupTypeName == "by_unit_and_weight")
            {
                var w = saleWeightKg ?? it.Quantity;
                return (w, 1000m, null);
            }

            return (saleUnits ?? it.Quantity, null, null);
        }

        /// <summary>Create or update order from WooCommerce plugin (API key auth). No AuthUser.</summary>
        public async Task<IApiResponse<OrderRes>> CreateOrUpdateOrderFromWooCommerceAsync(int siteId, WooCommerceOrderPayload payload, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            if (site == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Site not found.");
            var status = MapWooCommerceStatusToOurs(payload.Status);
            var externalId = !string.IsNullOrWhiteSpace(payload.OrderNumber) ? payload.OrderNumber : payload.ExternalOrderId?.ToString() ?? "";
            var existing = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, externalId, cancelToken).ConfigureAwait(false);
            var wcRequestJson = SerializeWooCommerceOrderPayloadForStorage(payload);
            var (wooBillingNotes, wooCustomerNote) = ResolveWooCommerceNotesForPersistence(payload);
            if (existing != null)
            {
                var previousWooStatus = existing.Status;
                var wcMainAddr = JoinMainDeliveryLine(payload.ShippingAddress?.Street, payload.ShippingAddress?.City, payload.ShippingAddress?.Zip);
                var sa = payload.ShippingAddress;
                var updateCustomer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                    siteId, site.AccountId, payload.Customer?.Phone ?? "", payload.Customer?.Name ?? "", email: payload.Customer?.Email,
                    city: sa?.City, defaultAddress: wcMainAddr, notes: null, marketingSms: null,
                    deliveryStreet: sa?.Street, deliveryApartment: sa?.Apartment, deliveryFloor: sa?.Floor, deliveryEntranceCode: sa?.ResolvedEntranceCode,
                    cancelToken).ConfigureAwait(false);
                var updated = await _orderStorage.UpdateOrderAsync(existing.Id, o =>
                {
                    o.Status = status;
                    o.CustomerName = payload.Customer?.Name;
                    o.CustomerPhone = payload.Customer?.Phone;
                    o.CustomerEmail = payload.Customer?.Email;
                    o.CustomerId = updateCustomer.Id;
                    ApplyWooCommerceShippingAddressToOrder(o, payload.ShippingAddress);
                    o.BillingNotes = wooBillingNotes;
                    o.CustomerNote = wooCustomerNote;
                    o.ManagerNote = null;
                    var pay = MapWooCommercePaymentMethodToInternal(payload.PaymentMethod, payload.PaymentMethodTitle, payload.GetResolvedPaymentLabel());
                    if (pay != null) o.PaymentMethod = pay;
                    ApplyWooCommerceStoredMetadata(o, payload);
                    o.WooCommerceRequestJson = wcRequestJson;
                    ApplyWooCommercePayloadColumnSnapshot(o, payload);
                    o.ShippingCost = payload.ShippingTotal;
                    o.Total = payload.OrderTotal ?? o.Total;
                    o.SubTotal = (payload.OrderTotal ?? o.SubTotal) - (payload.ShippingTotal ?? 0);
                    o.UpdatedDate = DateTime.UtcNow;
                    ApplyShippingInfoToOrder(o, payload.ShippingInfo, payload.GetResolvedShippingLabel());
                    NormalizeOrderDeliveryAndPickupDates(o);
                    ApplyWooCommercePickupStoreNote(o, payload.GetResolvedShippingStoreName());
                }, cancelToken).ConfigureAwait(false);
                if (updated == null)
                    return CreateResponse(response, StatusCode.ItemNotFound);
                var updateItems = new List<OrderItem>();
                if (payload.Items != null)
                {
                    for (var i = 0; i < payload.Items.Count; i++)
                    {
                        var it = payload.Items[i];
                        var ourProductId = await ResolveWooCommerceItemProductIdAsync(siteId, site.AccountId, it.ProductId, it.Sku, GetEffectiveVariationId(it), cancelToken).ConfigureAwait(false);
                        Product? product = ourProductId.HasValue ? await _productStorage.GetProductAsync(ourProductId.Value, cancelToken).ConfigureAwait(false) : null;
                        var matchedVariant = GetVariantFromPayloadItem(it, product);
                        var (qty, unitWeightGrams, variantTitle) = GetWooCommerceItemQuantityAndUnitWeight(it, product);
                        var oi = new OrderItem
                        {
                            OrderId = existing.Id,
                            ProductId = ourProductId ?? it.ProductId,
                            ProductVariantId = matchedVariant?.Id,
                            Title = it.Name,
                            VariantTitle = GetVariantTitleFromPayload(it) ?? variantTitle,
                            Quantity = qty,
                            UnitWeightGrams = unitWeightGrams,
                            PricePerUnit = it.UnitPrice,
                            TotalPrice = it.LineTotal,
                            Notes = !string.IsNullOrWhiteSpace(it.Note) ? it.Note : it.ProductNote,
                            SaleUnits = it.SaleUnits,
                            SaleTotalWeight = it.SaleTotalWeight,
                            WooCommerceProductId = it.ProductId,
                            WooCommerceVariationId = GetEffectiveVariationId(it),
                            SortOrder = i
                        };
                        PopulateWooCommerceOrderItemPayloadColumns(oi, it);
                        var mergeReq = new CreateOrderItemReq
                        {
                            ProductId = ourProductId ?? it.ProductId,
                            ProductVariantId = matchedVariant?.Id,
                            Quantity = qty,
                            UnitWeightGrams = unitWeightGrams,
                            SaleUnits = it.SaleUnits,
                            SaleTotalWeight = it.SaleTotalWeight,
                        };
                        OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, mergeReq, product);
                        ApplyWooCommerceQuantityTypeToLineDisplay(oi, it);
                        updateItems.Add(oi);
                    }
                }
                MergePickingStateIntoWooCommerceReplacementItems(
                    updateItems,
                    updated.OrderItem?.OrderBy(i => i.SortOrder).ToList());
                await _orderStorage.ReplaceOrderItemsAsync(existing.Id, updateItems, cancelToken).ConfigureAwait(false);
                var loaded = await _orderStorage.GetOrderByIdAsync(existing.Id, cancelToken).ConfigureAwait(false);
                await TryApplyCompletionInventoryWhenOrderCompletedAsync(existing.Id, previousWooStatus, loaded, cancelToken).ConfigureAwait(false);
                response.Data = _mapper.Map<OrderRes>(loaded!);
                return response;
            }
            var createItems = new List<CreateOrderItemReq>();
            if (payload.Items != null)
            {
                for (var i = 0; i < payload.Items.Count; i++)
                {
                    var it = payload.Items[i];
                    var ourProductId = await ResolveWooCommerceItemProductIdAsync(siteId, site.AccountId, it.ProductId, it.Sku, GetEffectiveVariationId(it), cancelToken).ConfigureAwait(false);
                    Product? product = ourProductId.HasValue ? await _productStorage.GetProductAsync(ourProductId.Value, cancelToken).ConfigureAwait(false) : null;
                    var matchedVariant = GetVariantFromPayloadItem(it, product);
                    var (qty, unitWeightGrams, variantTitle) = GetWooCommerceItemQuantityAndUnitWeight(it, product);
                    createItems.Add(new CreateOrderItemReq
                    {
                        ProductId = ourProductId ?? it.ProductId,
                        ProductVariantId = matchedVariant?.Id,
                        Title = it.Name,
                        VariantTitle = GetVariantTitleFromPayload(it) ?? variantTitle,
                        Quantity = qty,
                        UnitWeightGrams = unitWeightGrams,
                        PricePerUnit = it.UnitPrice,
                        TotalPrice = it.LineTotal,
                        Notes = !string.IsNullOrWhiteSpace(it.Note) ? it.Note : it.ProductNote,
                        SaleUnits = it.SaleUnits,
                        SaleTotalWeight = it.SaleTotalWeight,
                        WooCommerceProductId = it.ProductId,
                        WooCommerceVariationId = GetEffectiveVariationId(it),
                        SortOrder = i
                    });
                }
            }
            var req = new CreateOrderReq
            {
                SiteId = siteId,
                AccountId = site.AccountId,
                ExternalOrderId = externalId,
                Source = "WooCommerce",
                Status = status,
                CustomerName = payload.Customer?.Name,
                CustomerPhone = payload.Customer?.Phone,
                CustomerEmail = payload.Customer?.Email,
                CustomerNote = wooCustomerNote,
                ManagerNote = null,
                PaymentMethod = MapWooCommercePaymentMethodToInternal(payload.PaymentMethod, payload.PaymentMethodTitle, payload.GetResolvedPaymentLabel()),
                PaymentMethodTitle = payload.PaymentMethodTitle,
                PaymentLabel = payload.GetResolvedPaymentLabel(),
                ShippingLabel = payload.GetResolvedShippingLabel(),
                BillingNotes = wooBillingNotes,
                InternalOrderNotes = payload.InternalOrderNotes,
                WooCommerceSiteId = payload.SiteId,
                WooCommercePickupAffiliateId = payload.ShippingInfo?.PickupAffiliateId,
                ShippingCost = payload.ShippingTotal,
                Total = payload.OrderTotal,
                SubTotal = payload.OrderTotal - (payload.ShippingTotal ?? 0),
                Items = createItems
            };
            var wcMainAddrForCustomer = JoinMainDeliveryLine(payload.ShippingAddress?.Street, payload.ShippingAddress?.City, payload.ShippingAddress?.Zip);
            var saNew = payload.ShippingAddress;
            var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                req.SiteId, req.AccountId, req.CustomerPhone, req.CustomerName ?? "", email: req.CustomerEmail,
                city: saNew?.City, defaultAddress: wcMainAddrForCustomer, notes: null, marketingSms: null,
                deliveryStreet: saNew?.Street, deliveryApartment: saNew?.Apartment, deliveryFloor: saNew?.Floor, deliveryEntranceCode: saNew?.ResolvedEntranceCode,
                cancelToken).ConfigureAwait(false);
            var order = _mapper.Map<Order>(req);
            ApplyWooCommerceShippingAddressToOrder(order, payload.ShippingAddress);
            order.CustomerId = customer.Id;
            order.ManagerNote = null;
            // Use the date when the order was placed in WooCommerce, not when our API received the webhook
            order.CreationTime = payload.OrderDate.HasValue
                ? (payload.OrderDate.Value.Kind == DateTimeKind.Utc ? payload.OrderDate.Value : payload.OrderDate.Value.ToUniversalTime())
                : DateTime.UtcNow;
            order.CreationUserId = null;
            order.WooCommerceRequestJson = wcRequestJson;
            ApplyWooCommercePayloadColumnSnapshot(order, payload);
            ApplyShippingInfoToOrder(order, payload.ShippingInfo, payload.GetResolvedShippingLabel());
            NormalizeOrderDeliveryAndPickupDates(order);
            ApplyWooCommercePickupStoreNote(order, payload.GetResolvedShippingStoreName());
            var items = new List<OrderItem>();
            var wooProductCache = new Dictionary<int, Product?>();
            for (var i = 0; i < req.Items.Count; i++)
            {
                var lineReq = req.Items[i];
                var oi = _mapper.Map<OrderItem>(lineReq);
                oi.SortOrder = i;
                if (payload.Items != null && i < payload.Items.Count)
                    PopulateWooCommerceOrderItemPayloadColumns(oi, payload.Items[i]);
                if (lineReq.ProductId is > 0)
                {
                    if (!wooProductCache.TryGetValue(lineReq.ProductId.Value, out var p))
                    {
                        p = await _productStorage.GetProductAsync(lineReq.ProductId.Value, cancelToken).ConfigureAwait(false);
                        wooProductCache[lineReq.ProductId.Value] = p;
                    }
                    OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, lineReq, p);
                }
                if (payload.Items != null && i < payload.Items.Count)
                    ApplyWooCommerceQuantityTypeToLineDisplay(oi, payload.Items[i]);
                items.Add(oi);
            }
            var created = await _orderStorage.CreateOrderAsync(order, items, cancelToken).ConfigureAwait(false);
            var loadedOrder = await _orderStorage.GetOrderByIdAsync(created.Id, cancelToken).ConfigureAwait(false);
            await TryApplyWooIncomingOrderCatalogAndBaselinePickingAsync(loadedOrder!, cancelToken).ConfigureAwait(false);
            await TryApplyCompletionInventoryWhenOrderCompletedAsync(created.Id, previousStatus: null, loadedOrder, cancelToken).ConfigureAwait(false);
            await TrySendNewOrderCustomerSmsAsync(loadedOrder!, cancelToken).ConfigureAwait(false);
            if (loadedOrder != null)
                await TryEnqueueNewOrderAutoPrintAsync(loadedOrder, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loadedOrder!);
            return response;
        }

        /// <summary>
        /// Backend auto-print for new orders, so printing works even when no user has Orders page open.
        /// Enqueues idempotent job type <c>VoucherAuto:NewImmediate</c>.
        /// </summary>
        private async Task TryEnqueueNewOrderAutoPrintAsync(Order order, CancellationToken cancelToken)
        {
            if (!string.Equals(order.Status, "New", StringComparison.OrdinalIgnoreCase))
                return;

            var site = await _siteStorage.GetSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);
            if (site == null || site.AutoPrintEnabled != true || site.PrintNewOrderImmediate != true)
                return;

            // Ensure voucher is generated from a fully loaded order (including OrderItem rows).
            var orderForPrint = order;
            if (order.OrderItem == null || order.OrderItem.Count == 0)
            {
                var loaded = await _orderStorage.GetOrderByIdAsync(order.Id, cancelToken).ConfigureAwait(false);
                if (loaded != null)
                    orderForPrint = loaded;
            }

            var payload = BuildAutoVoucherHtml(orderForPrint);
            if (string.IsNullOrWhiteSpace(payload))
                return;

            var req = new CreatePrintJobReq
            {
                SiteId = order.SiteId,
                OrderId = order.Id,
                JobType = "VoucherAuto:NewImmediate",
                Trigger = "NewImmediate",
                ClientSource = "Backend:OrderService",
                Payload = payload
            };

            try
            {
                await _printJobService.CreateAsync(req, cancelToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue auto print job for new order {OrderId}.", order.Id);
            }
        }

        private string BuildAutoVoucherHtml(Order order)
        {
            var sb = new StringBuilder();
            var items = order.OrderItem?.OrderBy(i => i.SortOrder).ToList() ?? new List<OrderItem>();
            var customerName = order.CustomerName ?? "—";
            var customerPhone = order.CustomerPhone ?? "—";
            var orderNo = order.OrderNumber ?? order.Id.ToString(CultureInfo.InvariantCulture);
            var created = FormatOrderDateTime(order.CreationTime);
            var sourceLabel = VoucherSourceLabels.TryGetValue(order.Source ?? "", out var label) ? label : (order.Source ?? "");
            var sourceTop = string.IsNullOrWhiteSpace(sourceLabel) ? "" : $"מקור: {sourceLabel}";
            var isShipping = IsVoucherShipping(order);
            var deliveryDate = isShipping
                ? order.DeliveryDate
                : order.PickupDate;
            var deliveryTime = isShipping
                ? order.DeliveryTime
                : order.PickupTime;
            var newVoucher = string.Equals(order.Status, "New", StringComparison.OrdinalIgnoreCase);
            var pickingVoucher = string.Equals(order.Status, "InTreatment", StringComparison.OrdinalIgnoreCase);
            var showTopQr = newVoucher || pickingVoucher;
            var showBottomQr = !showTopQr;
            var orderNotes = CombineOrderLevelNotes(order);
            var grandTotal = ComputeVoucherGrandTotal(order);
            var qrDataUrl = GenerateVoucherQrDataUrl(order, _publicAppBaseUrl);
            var pa = "-webkit-print-color-adjust:exact;print-color-adjust:exact;";
            var qrInner = !string.IsNullOrWhiteSpace(qrDataUrl)
                ? $"<img src=\"{EscapeHtmlAttr(qrDataUrl)}\" alt=\"QR\" width=\"{VoucherPrintHtml.QrSizePx}\" height=\"{VoucherPrintHtml.QrSizePx}\" style=\"display:block;\" />"
                : $"<div style=\"width:{VoucherPrintHtml.QrSizePx}px;height:{VoucherPrintHtml.QrSizePx}px;background:#f3f4f6;\"></div>";
            var qrFrame =
                "<div dir=\"ltr\" style=\"display:flex;flex-direction:column;align-items:center;\">" +
                $"<div style=\"width:{VoucherPrintHtml.QrFramePx}px;height:{VoucherPrintHtml.QrFramePx}px;border:2px solid #000;box-sizing:border-box;display:flex;align-items:center;justify-content:center;background:#fff;\">" +
                qrInner +
                $"</div><div style=\"margin-top:2px;text-align:center;font-size:10px;line-height:13px;color:#000;\">{VoucherQrCaption}</div></div>";
            var headerShort = VoucherHeaderDeliveryShort(order);
            var headerCity = VoucherHeaderCityLine(order);
            var showHeaderCity = isShipping;
            var dateLabel = VoucherDateLabel;
            var timeLabel = VoucherTimeLabel;
            var itemCount = items.Count;
            var payLine = EscapeHtml(VoucherPaymentHeadline(order, !pickingVoucher));

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html dir=\"rtl\" lang=\"he\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
            sb.AppendLine($"  <title>בון הזמנה {EscapeHtml(orderNo)}</title>");
            sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\" />");
            sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=Heebo:wght@400;500;700;800;900&display=swap\" rel=\"stylesheet\" />");
            var pw = VoucherPrintHtml.PaperWidthMm;
            sb.AppendLine("  <style>");
            sb.AppendLine("    html, body { margin: 0; padding: 0; background: #fff; overflow: visible; overflow-x: visible; }");
            sb.AppendLine("    body { display: flex; flex-direction: column; align-items: center; width: 100%; box-sizing: border-box; }");
            sb.AppendLine($"    #voucher-root {{ max-width: {pw}mm; width: {pw}mm; min-width: 0; margin: 0; padding: {VoucherPrintHtml.InnerPadding}; box-sizing: border-box; font-family: Heebo, Arial, sans-serif; color: #000; background: #fff; font-size: 14px; overflow: visible; overflow-wrap: anywhere; word-break: break-word; line-break: anywhere; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}");
            sb.AppendLine("    #voucher-root *:not(img):not(svg):not(canvas) { overflow-wrap: anywhere; word-break: break-word; line-break: anywhere; max-width: 100%; min-width: 0; }");
            sb.AppendLine("    #voucher-root img { max-width: 100%; height: auto; }");
            sb.AppendLine($"    @media print {{ @page {{ size: {pw}mm auto; margin: 0; }} #voucher-root {{ width: {pw}mm; max-width: {pw}mm; padding: 2mm 2mm; -webkit-print-color-adjust: exact; print-color-adjust: exact; }} }}");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div id=\"voucher-root\">");
            if (!newVoucher)
            {
                var siteDisplay = order.Account?.Name?.Trim();
                if (!string.IsNullOrEmpty(siteDisplay))
                {
                    sb.Append("  <div style=\"margin-bottom:8px;border-bottom:1px solid #000;padding-bottom:8px;text-align:center;font-size:15px;font-weight:700;line-height:19px;\">");
                    sb.Append(EscapeHtml(siteDisplay));
                    sb.AppendLine("</div>");
                }
            }

            sb.Append("  <div style=\"display:flex;justify-content:space-between;align-items:flex-end;padding-bottom:8px;font-size:12px;line-height:13px;\">");
            sb.Append($"<span style=\"font-weight:400;\">{EscapeHtml(created)}</span>");
            sb.Append($"<span style=\"font-weight:700;\">{EscapeHtml(sourceTop)}</span>");
            sb.AppendLine("</div>");

            var headerCityBlock = showHeaderCity
                ? $"<div style=\"margin-top:8px;font-size:28px;font-weight:900;line-height:31px;letter-spacing:-0.5px;\">{EscapeHtml(string.IsNullOrEmpty(headerCity) ? "—" : headerCity)}</div>"
                : "";
            var headerCore =
                $"<div style=\"font-size:28px;font-weight:900;line-height:32px;\">#{EscapeHtml(orderNo)}</div>" +
                $"<div style=\"font-size:32px;font-weight:900;line-height:36px;letter-spacing:-0.5px;\">{EscapeHtml(headerShort)}</div>" +
                headerCityBlock;

            if (showTopQr)
            {
                sb.Append("  <div style=\"display:flex;justify-content:space-between;align-items:flex-start;gap:12px;margin-bottom:12px;padding-bottom:16px;border-bottom:1px solid #000;\">");
                sb.Append("<div style=\"flex:1;min-width:0;text-align:right;\">");
                sb.Append(headerCore);
                sb.Append("</div>");
                sb.Append(qrFrame);
                sb.AppendLine("</div>");
            }
            else
            {
                sb.Append("  <div style=\"margin-bottom:12px;padding-bottom:16px;border-bottom:1px solid #000;text-align:right;\">");
                sb.Append(headerCore);
                sb.AppendLine("</div>");
            }

            if (deliveryDate.HasValue || !string.IsNullOrWhiteSpace(deliveryTime))
            {
                sb.Append("  <div style=\"margin-bottom:12px;padding-bottom:12px;border-bottom:1px solid #000;\">");
                sb.Append("<div style=\"display:flex;justify-content:space-between;font-size:13px;line-height:18px;margin-bottom:4px;\">");
                sb.Append($"<span>{EscapeHtml(dateLabel)}</span><span>{EscapeHtml(timeLabel)}</span></div>");
                sb.Append("<div style=\"display:flex;justify-content:space-between;align-items:center;gap:8px;\">");
                sb.Append(deliveryDate.HasValue
                    ? $"<span style=\"font-size:20px;font-weight:700;line-height:24px;\">{EscapeHtml(FormatVoucherDateWithWeekday(deliveryDate.Value))}</span>"
                    : "<span style=\"font-size:20px;font-weight:700;\">—</span>");
                sb.Append(!string.IsNullOrWhiteSpace(deliveryTime)
                    ? $"<span style=\"font-size:20px;font-weight:700;line-height:24px;\">{EscapeHtml(deliveryTime!)}</span>"
                    : "<span style=\"font-size:20px;font-weight:700;\">—</span>");
                sb.AppendLine("</div></div>");
            }

            sb.Append("  <div style=\"display:flex;justify-content:space-between;align-items:flex-start;gap:8px;margin-bottom:12px;\">");
            sb.Append($"<div style=\"flex:1;min-width:0;text-align:right;font-size:28px;font-weight:900;line-height:35px;letter-spacing:-0.5px;\">{EscapeHtml(customerName)}</div>");
            sb.Append($"<div dir=\"ltr\" style=\"flex-shrink:0;padding-top:4px;font-size:16px;font-weight:500;line-height:20px;\">{EscapeHtml(customerPhone)}</div>");
            sb.AppendLine("</div>");

            if (!string.IsNullOrWhiteSpace(orderNotes))
                sb.AppendLine($"  <div style=\"margin-bottom:12px;font-size:14px;font-weight:bold;line-height:1.45;\">{VoucherNotesLabel} {EscapeHtml(orderNotes)}</div>");

            if (isShipping) AppendVoucherShippingAddressHtml(sb, order, pa);

            sb.AppendLine("  <div style=\"margin-bottom:8px;padding:8px 0;border-bottom:1px solid #000;text-align:center;\">");
            sb.AppendLine($"    <span style=\"font-size:20px;font-weight:800;line-height:24px;\">{VoucherItemsTitle} ({itemCount})</span>");
            sb.AppendLine("  </div>");

            var attrOpts = new OrderItemAttributeDisplayOptions { OmitOrderLineSizeLabel = true };
            foreach (var it in items)
            {
                var title = EscapeHtml(OrderItemLineDisplay.GetOrderItemProductName(it));
                var qtyStr = EscapeHtml(OrderItemLineDisplay.FormatOrderItemQuantityBadge(it));
                sb.Append("  <div style=\"display:flex;justify-content:space-between;align-items:flex-start;gap:8px;padding:8px 0;border-bottom:1px dashed #000;\">");
                sb.Append($"<div style=\"flex-shrink:0;white-space:nowrap;padding-top:2px;text-align:right;font-size:17px;font-weight:900;line-height:22px;\">{qtyStr}</div>");
                sb.Append("<div style=\"flex:1;min-width:0;text-align:right;\">");
                sb.Append($"<div style=\"font-size:17px;font-weight:700;line-height:19px;\">{title}</div>");
                foreach (var seg in OrderItemLineDisplay.GetOrderItemAttributeSegments(it, attrOpts))
                {
                    sb.Append("<div style=\"padding-right:12px;font-size:11px;font-weight:400;line-height:15px;\">• ");
                    sb.Append(EscapeHtml(seg));
                    sb.Append("</div>");
                }

                var lineAmt = GetVoucherPickedLineAmount(it);
                var loket = BuildVoucherLoketLine(it, lineAmt);
                if (!string.IsNullOrEmpty(loket))
                {
                    sb.Append("<div style=\"padding-right:12px;font-size:11px;font-weight:700;line-height:15px;\">");
                    sb.Append(EscapeHtml(loket));
                    sb.Append("</div>");
                }
                else
                {
                    var legacyHint = OrderItemLineDisplay.FormatVoucherLegacyUnitWeightHint(it, newVoucher);
                    if (!string.IsNullOrWhiteSpace(legacyHint))
                    {
                        sb.Append("<div style=\"padding-right:12px;font-size:11px;font-weight:400;line-height:15px;\">");
                        sb.Append(EscapeHtml(legacyHint));
                        sb.Append("</div>");
                    }
                }

                if (!string.IsNullOrWhiteSpace(it.Notes))
                {
                    sb.Append("<div style=\"padding-right:12px;font-size:11px;font-weight:700;line-height:15px;\">הערה: ");
                    sb.Append(EscapeHtml(it.Notes!));
                    sb.Append("</div>");
                }

                sb.Append("</div></div>");
            }

            if (!newVoucher && grandTotal.HasValue)
            {
                sb.AppendLine("  <div style=\"margin-bottom:12px;padding-bottom:12px;border-bottom:1px dashed #000;text-align:center;\">");
                sb.AppendLine($"    <div style=\"font-size:15px;font-weight:700;line-height:19px;\">{payLine}</div>");
                sb.AppendLine($"    <div style=\"margin-top:4px;font-size:24px;font-weight:700;line-height:19px;direction:ltr;unicode-bidi:embed;\">₪{grandTotal.Value.ToString("0.00", CultureInfo.InvariantCulture)}</div>");
                sb.AppendLine($"    <div style=\"margin-top:4px;font-size:11px;font-weight:400;line-height:19px;\">{VoucherVatIncludedLabel}</div>");
                sb.AppendLine("  </div>");
            }

            if (showBottomQr)
            {
                sb.Append("  <div style=\"margin-bottom:12px;padding-bottom:12px;border-bottom:1px solid #000;display:flex;justify-content:center;\">");
                sb.Append(qrFrame);
                sb.AppendLine("</div>");
            }

            sb.AppendLine("  <div style=\"padding-top:8px;text-align:center;font-size:13px;font-weight:700;line-height:16px;letter-spacing:normal;\">");
            sb.AppendLine(EscapeHtml(string.IsNullOrWhiteSpace(order.ShippingStoreName) ? VoucherBrandFooter : order.ShippingStoreName.Trim()));
            sb.AppendLine("  </div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        /// <summary>Sunday = 0 … Saturday = 6 (same order as <see cref="DayOfWeek"/>).</summary>
        private static readonly string[] HebrewWeekdayNames =
        {
            "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת",
        };

        /// <summary>Voucher / print: e.g. <c>שני 11/03/26</c>.</summary>
        private static string FormatVoucherDateWithWeekday(DateTime date)
        {
            var dayName = HebrewWeekdayNames[(int)date.DayOfWeek];
            var shortDate = date.ToString("dd/MM/yy", CultureInfo.InvariantCulture);
            return $"{dayName} {shortDate}";
        }

        private static string FormatOrderDateTime(DateTime creationTime)
        {
            var local = creationTime.ToLocalTime();
            var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
            return $"{FormatVoucherDateWithWeekday(local)} {time}";
        }

        private static string CombineOrderLevelNotes(Order order)
        {
            var parts = new[] { order.ManagerNote, order.DeliveryNote, order.CustomerNote }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim());
            return string.Join(" · ", parts);
        }

        /// <summary>Match shop-manager <c>getDeliveryMainLine</c> / voucher shipping main line.</summary>
        private static string? GetDeliveryMainLineForVoucher(Order order)
        {
            var street = order.DeliveryStreet?.Trim() ?? "";
            var city = order.DeliveryCity?.Trim() ?? "";
            if (!string.IsNullOrEmpty(street) || !string.IsNullOrEmpty(city))
            {
                var segs = new List<string>();
                if (!string.IsNullOrEmpty(street)) segs.Add(street);
                if (!string.IsNullOrEmpty(city)) segs.Add(city);
                return string.Join(", ", segs);
            }

            return string.IsNullOrWhiteSpace(order.DeliveryAddress) ? null : order.DeliveryAddress.Trim();
        }

        /// <summary>Match <c>getShippingAddressPartsForVoucher</c> for auto-print HTML.</summary>
        private static ShippingVoucherParts? TryGetShippingAddressPartsForVoucher(Order order)
        {
            if (!IsVoucherShipping(order))
                return null;

            var apt = order.DeliveryApartment?.Trim();
            var fl = order.DeliveryFloor?.Trim();
            var code = order.DeliveryEntranceCode?.Trim();
            var mainLine = GetDeliveryMainLineForVoucher(order);
            var raw = order.DeliveryAddress?.Trim() ?? "";

            if (!string.IsNullOrEmpty(apt) || !string.IsNullOrEmpty(fl) || !string.IsNullOrEmpty(code))
            {
                if (string.IsNullOrEmpty(mainLine) && string.IsNullOrEmpty(apt) && string.IsNullOrEmpty(fl) && string.IsNullOrEmpty(code))
                    return null;
                return new ShippingVoucherParts
                {
                    Main = string.IsNullOrEmpty(mainLine) ? null : mainLine,
                    Apartment = string.IsNullOrEmpty(apt) ? null : apt,
                    Floor = string.IsNullOrEmpty(fl) ? null : fl,
                    EntranceCode = string.IsNullOrEmpty(code) ? null : code,
                };
            }

            var legacySource = !string.IsNullOrEmpty(mainLine) ? mainLine : raw;
            if (string.IsNullOrEmpty(legacySource))
                return null;

            var parts = legacySource.Split(new[] { ", " }, StringSplitOptions.None)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();

            if (parts.Count >= 5)
            {
                return new ShippingVoucherParts
                {
                    Main = $"{parts[0]}, {parts[1]}",
                    Apartment = parts[2],
                    Floor = parts[3],
                    EntranceCode = parts[4],
                };
            }

            if (parts.Count == 4)
            {
                return new ShippingVoucherParts
                {
                    Main = $"{parts[0]}, {parts[1]}",
                    Apartment = parts[2],
                    Floor = parts[3],
                };
            }

            if (parts.Count == 3)
            {
                return new ShippingVoucherParts
                {
                    Main = $"{parts[0]}, {parts[1]}",
                    Apartment = parts[2],
                };
            }

            return new ShippingVoucherParts { Main = legacySource };
        }

        private static void AppendVoucherShippingAddressHtml(StringBuilder sb, Order order, string pa)
        {
            var ship = TryGetShippingAddressPartsForVoucher(order);
            if (ship == null
                || (string.IsNullOrEmpty(ship.Main)
                    && string.IsNullOrEmpty(ship.Apartment)
                    && string.IsNullOrEmpty(ship.Floor)
                    && string.IsNullOrEmpty(ship.EntranceCode)))
                return;

            var extras = FormatShippingExtrasCommaLine(ship);
            sb.Append("  <div style=\"margin-bottom:12px;border-bottom:1px solid #000;padding-bottom:12px;text-align:right;");
            sb.Append(pa);
            sb.Append($"\"><div style=\"font-size:13px;font-weight:400;line-height:18px;letter-spacing:0.325px;\">{VoucherShippingLabel}</div>");
            if (!string.IsNullOrEmpty(ship.Main))
            {
                sb.Append("<div style=\"margin-top:4px;font-size:15px;font-weight:700;line-height:20px;");
                sb.Append(pa);
                sb.Append("\">");
                sb.Append(EscapeHtml(ship.Main));
                sb.Append("</div>");
            }

            if (!string.IsNullOrEmpty(extras))
            {
                sb.Append("<div style=\"margin-top:4px;font-size:15px;font-weight:400;line-height:20px;");
                sb.Append(pa);
                sb.Append("\">");
                sb.Append(EscapeHtml(extras));
                sb.Append("</div>");
            }

            sb.AppendLine("</div>");
        }

        private static string FormatShippingExtrasCommaLine(ShippingVoucherParts ship)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(ship.Apartment)) parts.Add($"דירה {ship.Apartment}");
            if (!string.IsNullOrEmpty(ship.Floor)) parts.Add($"קומה {ship.Floor}");
            if (!string.IsNullOrEmpty(ship.EntranceCode)) parts.Add($"קוד כניסה {ship.EntranceCode}");
            return string.Join(", ", parts);
        }

        private static string VoucherHeaderDeliveryShort(Order order)
        {
            if (IsVoucherShipping(order)) return "משלוח";
            if (IsVoucherPickup(order)) return "איסוף עצמי";
            return order.DeliveryType?.Trim() ?? "";
        }

        private static bool IsVoucherShipping(Order order)
        {
            var d = order.DeliveryType?.Trim() ?? "";
            if (string.IsNullOrEmpty(d)) return false;
            return string.Equals(d, "Shipping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(d, "Express", StringComparison.OrdinalIgnoreCase)
                || d.Contains("משלוח", StringComparison.Ordinal);
        }

        private static bool IsVoucherPickup(Order order)
        {
            var d = order.DeliveryType?.Trim() ?? "";
            if (string.IsNullOrEmpty(d)) return false;
            return string.Equals(d, "Pickup", StringComparison.OrdinalIgnoreCase)
                || d.Contains("איסוף", StringComparison.Ordinal);
        }

        private static string VoucherHeaderCityLine(Order order)
        {
            var c = order.DeliveryCity?.Trim();
            if (!string.IsNullOrEmpty(c)) return c;
            var main = GetDeliveryMainLineForVoucher(order);
            if (string.IsNullOrEmpty(main)) return order.ShippingStoreName?.Trim() ?? "";
            var parts = main.Split(new[] { ", " }, StringSplitOptions.None).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count >= 2) return parts[^1];
            return order.ShippingStoreName?.Trim() ?? "";
        }

        private static string VoucherPaymentHeadline(Order order, bool settled = true)
        {
            var m = order.PaymentMethod?.Trim() ?? "";
            if (string.IsNullOrEmpty(m)) return settled ? "שולם" : "לתשלום";
            if (string.Equals(m, "Cash", StringComparison.OrdinalIgnoreCase) || m == "מזומן")
                return settled ? "שולם במזומן" : "תשלום במזומן";
            var lower = m.ToLowerInvariant();
            if (lower.Contains("card", StringComparison.Ordinal) || lower.Contains("credit", StringComparison.Ordinal) ||
                string.Equals(m, "SavedCard", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("אשראי", StringComparison.Ordinal))
                return settled ? "שולם באשראי" : "תשלום באשראי";
            return settled ? $"שולם ({m})" : $"תשלום ({m})";
        }

        private static string? BuildVoucherLoketLine(OrderItem it, decimal? lineAmt)
        {
            if (!it.PickedQuantity.HasValue || it.PickedQuantity.Value <= 0m) return null;
            var qty = OrderItemLineDisplay.FormatVoucherPickedDisplay(it);
            if (lineAmt.HasValue) return $"לוקט: {qty} | ₪{lineAmt.Value.ToString("0.00", CultureInfo.InvariantCulture)}";
            return $"לוקט: {qty}";
        }

        private static string EscapeHtmlAttr(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
        }

        private sealed class ShippingVoucherParts
        {
            public string? Main { get; init; }
            public string? Apartment { get; init; }
            public string? Floor { get; init; }
            public string? EntranceCode { get; init; }
        }

        /// <summary>Match shop-manager <c>getVoucherPickedLineAmount</c>: line total after pick (TotalPrice or picked × PricePerUnit).</summary>
        private static decimal? GetVoucherPickedLineAmount(OrderItem item)
        {
            if (!item.PickedQuantity.HasValue || item.PickedQuantity.Value <= 0m) return null;
            if (item.TotalPrice.HasValue) return item.TotalPrice.Value;
            return item.PickedQuantity.Value * (item.PricePerUnit ?? 0m);
        }

        private static decimal? ComputeVoucherGrandTotal(Order order)
        {
            var items = order.OrderItem ?? new List<OrderItem>();
            var shipping = order.ShippingCost ?? 0m;
            var anyPicked = items.Any(i => i.PickedQuantity.HasValue && i.PickedQuantity.Value > 0m);
            decimal itemsSum;
            if (anyPicked)
            {
                // After picking: compute from actual picked line totals — order.Total is the original pre-picking value and is not updated after picking.
                itemsSum = items.Sum(i => GetVoucherPickedLineAmount(i) ?? 0m);
            }
            else
            {
                // No picking done: prefer order.Total (e.g. from WooCommerce), otherwise compute from item prices.
                if (order.Total.HasValue) return order.Total.Value;
                itemsSum = items.Sum(i => i.TotalPrice ?? i.Quantity * (i.PricePerUnit ?? 0m));
            }

            if (itemsSum <= 0m && shipping <= 0m) return null;
            return itemsSum + shipping;
        }

        private static string GenerateVoucherQrDataUrl(Order order, string? publicBaseUrl)
        {
            try
            {
                var payload = BuildPickingUrl(order.Id, publicBaseUrl);
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                var png = new PngByteQRCode(data);
                var bytes = png.GetGraphic(8, drawQuietZones: true);
                return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildPickingUrl(int orderId, string? publicBaseUrl)
        {
            var pickingPath = $"/orderpicking?orderId={orderId}";
            if (string.IsNullOrWhiteSpace(publicBaseUrl))
                return pickingPath;
            return $"{publicBaseUrl.TrimEnd('/')}{pickingPath}";
        }

        private static string? ResolvePublicAppBaseUrl(IConfiguration configuration)
        {
            var configured =
                configuration["App:PublicBaseUrl"] ??
                configuration["PublicAppBaseUrl"] ??
                configuration["Client:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured))
                return configured!.Trim();

            // Fallback: use first configured CORS origin when explicit public URL is not set.
            var origins = configuration["Cors:AllowedOrigins"];
            if (string.IsNullOrWhiteSpace(origins))
                return null;
            var first = origins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? null : first;
        }

        private static string EscapeHtml(string s)
        {
            return s
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&#39;", StringComparison.Ordinal);
        }

        /// <summary>Record payment from WooCommerce (invoice, Cardcom JSON, gateway status). API key auth. Order: <c>orderNumber</c> and/or <c>orderId</c> (WooCommerce id) must match <see cref="Order.ExternalOrderId"/>.</summary>
        public async Task<IApiResponse<OrderRes>> RecordPaymentFromWooCommerceAsync(int siteId, WooCommerceOrderPaymentPayload payment, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            payment.NormalizeWooCommercePaymentRequest();
            var externalKey = payment.ResolveExternalOrderKey();
            if (string.IsNullOrWhiteSpace(externalKey))
            {
                _logger.LogWarning(
                    "WooCommerce OrderPayment rejected: orderNumber, orderId, and externalOrderId missing. siteId={SiteId}",
                    siteId);
                return CreateResponse(response, StatusCode.InvalidRequest, "orderNumber, orderId, or externalOrderId is required.");
            }
            var order = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, externalKey, cancelToken).ConfigureAwait(false);
            if (order == null)
            {
                _logger.LogWarning(
                    "WooCommerce OrderPayment: order not found for external key. siteId={SiteId}, externalOrderKey={ExternalOrderKey}, gatewayStatus={GatewayStatus}",
                    siteId, externalKey, payment.Status);
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            }
            var treatAsPaid = IsSuccessfulWooCommerceGatewayPaymentStatus(payment.Status)
                && HasWooCommercePaidTransactionWhenRequired(payment);
            var cardcomJson = SerializeCardcomPaymentToken(payment.CardcomPayment);
            var cardcomChars = cardcomJson?.Length ?? 0;
            _logger.LogInformation(
                "WooCommerce OrderPayment processing. siteId={SiteId}, externalOrderKey={ExternalOrderKey}, internalOrderId={InternalOrderId}, gatewayStatus={GatewayStatus}, treatAsPaid={TreatAsPaid}, cardcomJsonChars={CardcomChars}, hasInvoice={HasInvoice}",
                siteId, externalKey, order.Id, payment.Status, treatAsPaid, cardcomChars, !string.IsNullOrWhiteSpace(payment.InvoiceNumber));
            var updated = await _orderStorage.UpdateOrderAsync(order.Id, o =>
            {
                if (payment.InvoiceNumber != null) o.InvoiceNumber = payment.InvoiceNumber;
                if (payment.PaymentReference != null || payment.ClearanceNumber != null)
                    o.PaymentReference = payment.PaymentReference ?? payment.ClearanceNumber;
                if (treatAsPaid && payment.PaidAt.HasValue) o.PaidAt = payment.PaidAt;
                if (treatAsPaid) o.PaymentStatus = "Paid";
                if (!string.IsNullOrWhiteSpace(payment.Status)) o.ExternalPaymentStatus = payment.Status.Trim();
                if (!string.IsNullOrWhiteSpace(cardcomJson)) o.CardcomPaymentJson = cardcomJson;
                o.UpdatedDate = DateTime.UtcNow;
            }, cancelToken).ConfigureAwait(false);
            if (updated == null)
            {
                _logger.LogWarning(
                    "WooCommerce OrderPayment: UpdateOrderAsync returned null. siteId={SiteId}, internalOrderId={InternalOrderId}, externalOrderKey={ExternalOrderKey}",
                    siteId, order.Id, externalKey);
                return CreateResponse(response, StatusCode.ItemNotFound);
            }
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loaded!);
            _logger.LogInformation(
                "WooCommerce OrderPayment completed. siteId={SiteId}, internalOrderId={InternalOrderId}, externalOrderKey={ExternalOrderKey}, paymentStatus={PaymentStatus}, externalPaymentStatus={ExternalPaymentStatus}",
                siteId, loaded!.Id, externalKey, loaded.PaymentStatus, loaded.ExternalPaymentStatus);
            return response;
        }

        private static string? SerializeCardcomPaymentToken(JToken? cardcom)
        {
            if (cardcom == null || cardcom.Type == JTokenType.Null) return null;
            return cardcom.ToString(Formatting.None);
        }

        /// <summary>When <paramref name="status"/> is empty, treat as success (legacy webhooks). Otherwise avoid marking paid on clear failure tokens.</summary>
        private static bool IsSuccessfulWooCommerceGatewayPaymentStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return true;
            var s = status.Trim().ToLowerInvariant();
            if (s is "failed" or "fail" or "error" or "declined" or "rejected" or "cancelled" or "canceled") return false;
            if (s.Contains("declin", StringComparison.Ordinal) || s.Contains("fail", StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>When root <c>payment</c> is sent, a non-empty <c>transactionId</c> is required to mark paid; legacy bodies without <c>payment</c> unchanged.</summary>
        private static bool HasWooCommercePaidTransactionWhenRequired(WooCommerceOrderPaymentPayload payment)
        {
            if (!payment.RequiresGatewayTransactionIdForPaid())
                return true;
            return !string.IsNullOrWhiteSpace(payment.ResolveGatewayTransactionIdForPaid());
        }

        /// <summary>
        /// WooCommerce webhooks rebuild all lines via <see cref="OrderStorage.ReplaceOrderItemsAsync"/>, which would drop
        /// <see cref="OrderItem.PickedQuantity"/> / picked line totals. Copy them from the previous rows when line identity matches.
        /// </summary>
        private static void MergePickingStateIntoWooCommerceReplacementItems(
            List<OrderItem> newItems,
            List<OrderItem>? previousItemsOrdered)
        {
            if (newItems.Count == 0 || previousItemsOrdered == null || previousItemsOrdered.Count == 0)
                return;
            var oldOrdered = previousItemsOrdered.OrderBy(i => i.SortOrder).ToList();
            var n = Math.Min(newItems.Count, oldOrdered.Count);
            for (var i = 0; i < n; i++)
            {
                var line = newItems[i];
                var prev = oldOrdered[i];
                if (!SameWooCommerceLineIdentityForPickingMerge(line, prev))
                    continue;
                if (!prev.PickedQuantity.HasValue || prev.PickedQuantity.Value <= 0m)
                    continue;
                line.PickedQuantity = prev.PickedQuantity;
                line.TotalPrice = prev.TotalPrice;
                line.PickingUserConfirmed = prev.PickingUserConfirmed;
            }
        }

        private static bool SameWooCommerceLineIdentityForPickingMerge(OrderItem a, OrderItem b)
        {
            if (a.WooCommerceProductId != b.WooCommerceProductId)
                return false;
            return (a.WooCommerceVariationId ?? 0) == (b.WooCommerceVariationId ?? 0);
        }

        /// <summary>
        /// When an order first reaches <c>Completed</c>, reduce catalog stock for lines that never had <see cref="OrderItem.PickedQuantity"/> set,
        /// using <see cref="OrderItem.Quantity"/> (ordered amount). Lines with picking use stock updates from <see cref="UpdatePickingAsync"/> only.
        /// Idempotent via <see cref="Order.CompletionInventoryApplied"/>.
        /// </summary>
        private async Task TryApplyCompletionInventoryWhenOrderCompletedAsync(
            int orderId,
            string? previousStatus,
            Order? orderWithItems,
            CancellationToken cancelToken)
        {
            if (orderWithItems == null) return;
            if (orderWithItems.CompletionInventoryApplied) return;
            if (!string.Equals(orderWithItems.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(previousStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                return;

            var adjustedProductIds = await ApplyOrderedQuantityToCatalogForUnpickedLinesAsync(orderWithItems, cancelToken).ConfigureAwait(false);
            await _orderStorage.SetOrderCompletionInventoryAppliedAsync(orderId, true, cancelToken).ConfigureAwait(false);
            if (adjustedProductIds.Count > 0)
            {
                _logger.LogInformation(
                    "Catalog stock adjusted on order completion (ordered qty for lines without picking). orderId={OrderId}, lines={LineCount}",
                    orderId, adjustedProductIds.Count);
                await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                    orderWithItems.SiteId,
                    adjustedProductIds,
                    "order completion (unpicked lines)",
                    cancelToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// New WooCommerce orders: reduce Store catalog by ordered line quantities (Woo already reduced on the site),
        /// then set each line's picked qty baseline to ordered qty so further picking adjusts only the delta.
        /// Skips when order is already Completed (completion handler applies) or when completion flag was set earlier.
        /// </summary>
        private async Task TryApplyWooIncomingOrderCatalogAndBaselinePickingAsync(Order order, CancellationToken cancelToken)
        {
            if (!string.Equals(order.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return;
            if (order.CompletionInventoryApplied)
                return;
            var lines = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            if (lines.Count == 0) return;
            var productIds = new List<int>();
            foreach (var line in lines)
            {
                if (line.ProductId is not > 0 || line.Quantity <= 0m) continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        line.ProductId.Value,
                        line.ProductVariantId,
                        line.Quantity,
                        cancelToken)
                    .ConfigureAwait(false);
                productIds.Add(line.ProductId.Value);
            }
            if (productIds.Count == 0) return;
            await _orderStorage.SetOrderedCatalogConsumedAndBaselinePickingAsync(order.Id, cancelToken).ConfigureAwait(false);
            await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                order.SiteId,
                productIds,
                "Woo order ingest (align Store catalog with ordered qty)",
                cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Phone/Kiosk/Website orders: reduce Store catalog by ordered line quantities at creation, baseline <see cref="OrderItem.PickedQuantity"/>
        /// so picking adjusts only the delta. Skips WooCommerce (handled by <see cref="TryApplyWooIncomingOrderCatalogAndBaselinePickingAsync"/>).
        /// </summary>
        private async Task TryApplyInternalOrderCatalogOnCreateAsync(Order order, CancellationToken cancelToken)
        {
            if (string.Equals(order.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return;
            if (order.CompletionInventoryApplied)
                return;
            var lines = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            if (lines.Count == 0) return;
            var productIds = new List<int>();
            foreach (var line in lines)
            {
                if (line.ProductId is not > 0 || line.Quantity <= 0m) continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        line.ProductId.Value,
                        line.ProductVariantId,
                        line.Quantity,
                        cancelToken)
                    .ConfigureAwait(false);
                productIds.Add(line.ProductId.Value);
            }
            if (productIds.Count == 0) return;
            await _orderStorage.SetOrderedCatalogConsumedAndBaselinePickingAsync(order.Id, cancelToken).ConfigureAwait(false);
            await ScheduleWooCommerceCatalogStockPushForProductsAsync(
                order.SiteId,
                productIds,
                "internal order create (ordered qty)",
                cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores catalog stock when an order is cancelled: reverses picked consumption and/or completion-only line consumption.
        /// PickedQuantity &gt; 0 means that much was net-consumed via create+baseline and picking; unpicked completion used Quantity with PickedQuantity unset.
        /// </summary>
        private async Task<List<int>> TryRestoreCatalogStockOnOrderCancelAsync(Order order, CancellationToken cancelToken)
        {
            var pushIds = new List<int>();
            var lines = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            foreach (var line in lines)
            {
                if (line.ProductId is not > 0) continue;
                decimal restoreQty = 0m;
                if (line.PickedQuantity.HasValue && line.PickedQuantity.Value > 0m)
                    restoreQty = line.PickedQuantity.Value;
                else if (!line.PickedQuantity.HasValue && order.CompletionInventoryApplied && line.Quantity > 0m)
                    restoreQty = line.Quantity;
                else
                    continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        line.ProductId.Value,
                        line.ProductVariantId,
                        -restoreQty,
                        cancelToken)
                    .ConfigureAwait(false);
                pushIds.Add(line.ProductId.Value);
            }
            return pushIds.Distinct().ToList();
        }

        private async Task<List<int>> ApplyOrderedQuantityToCatalogForUnpickedLinesAsync(Order order, CancellationToken cancelToken)
        {
            var productIds = new List<int>();
            foreach (var line in order.OrderItem?.Where(i => !i.IsDeleted) ?? Enumerable.Empty<OrderItem>())
            {
                if (line.ProductId is not > 0) continue;
                if (line.PickedQuantity.HasValue)
                    continue;
                if (line.Quantity <= 0m) continue;
                await _productStorage
                    .ApplyPickingConsumptionDeltaAsync(
                        line.ProductId.Value,
                        line.ProductVariantId,
                        line.Quantity,
                        cancelToken)
                    .ConfigureAwait(false);
                productIds.Add(line.ProductId.Value);
            }

            return productIds.Distinct().ToList();
        }

        private static string MapWooCommerceStatusToOurs(string? wc)
        {
            if (string.IsNullOrWhiteSpace(wc)) return "New";
            var s = wc.Trim();
            if (string.Equals(s, "completed", StringComparison.OrdinalIgnoreCase)) return "Completed";
            if (string.Equals(s, "cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "canceled", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
            return "New";
        }

        /// <summary>Maps our order status to WooCommerce/oc-storeos status for sync. Returns null if no mapping (skip sync).</summary>
        private static string? MapOurStatusToWooCommerce(string? ourStatus)
        {
            if (string.IsNullOrWhiteSpace(ourStatus)) return null;
            var s = ourStatus.Trim();
            if (string.Equals(s, "New", StringComparison.OrdinalIgnoreCase)) return "on-hold";
            if (string.Equals(s, "InTreatment", StringComparison.OrdinalIgnoreCase)) return "processing";
            if (string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase)) return "completed";
            if (string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "cancelled";
            return null;
        }
    }
}
