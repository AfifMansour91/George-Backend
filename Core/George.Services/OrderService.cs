using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

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
        private readonly WooCommerceService _wooCommerceService;

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
            WooCommerceService wooCommerceService)
            : base(logger, mapper, cache)
        {
            _orderStorage = orderStorage;
            _customerStorage = customerStorage;
            _siteStorage = siteStorage;
            _accountStorage = accountStorage;
            _productStorage = productStorage;
            _smsProvider = smsProvider;
            _wooCommerceService = wooCommerceService;
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
            if (req.Items.Any(i => (i.ProductId ?? 0) <= 0))
                return CreateResponse(response, StatusCode.InvalidRequest, "Each order item must have a valid ProductId.");
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

            // Ensure customer exists for this site (find by SiteId + phone, or create); then link order to that customer. Pass marketingSms so it is persisted on the customer.
            var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                req.SiteId,
                req.AccountId,
                req.CustomerPhone,
                req.CustomerName!,
                email: req.CustomerEmail,
                city: null,
                defaultAddress: req.DeliveryAddress,
                notes: null,
                marketingSms: req.MarketingSms,
                cancelToken).ConfigureAwait(false);

            var order = _mapper.Map<Order>(req);
            order.CustomerId = customer.Id; // always set: customer was either found or created above
            order.CreationTime = DateTime.UtcNow;
            order.CreationUserId = AuthUser.Id;
            order.IsDeleted = false;
            var todayUtc = DateTime.UtcNow.Date;
            if (!order.DeliveryDate.HasValue) order.DeliveryDate = todayUtc;
            if (!order.PickupDate.HasValue) order.PickupDate = todayUtc;
            var items = new List<OrderItem>();
            for (var i = 0; i < req.Items.Count; i++)
            {
                var oi = _mapper.Map<OrderItem>(req.Items[i]);
                oi.SortOrder = i;
                items.Add(oi);
            }
            var created = await _orderStorage.CreateOrderAsync(order, items, cancelToken);
            var loaded = await _orderStorage.GetOrderByIdAsync(created.Id, cancelToken);
            await TrySendNewOrderCustomerSmsAsync(loaded!, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loaded);
            return response;
        }

        /// <summary>Sprint 2: Send customer SMS for new order (Kiosk or Phone) using notification settings. Does not fail the request on SMS errors.</summary>
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

            string? template = null;
            var deliveryType = (order.DeliveryType ?? "").Trim();
            var source = (order.Source ?? "").Trim();
            if (string.Equals(deliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessageShipping;
            else if (string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessagePickup;
            else if (string.Equals(source, "Kiosk", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessageKiosk;
            else
                template = settings.OrderReadyCustomerMessagePickup ?? settings.OrderReadyCustomerMessageShipping;

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

        private static string ReplaceOrderPlaceholders(string template, Order order)
        {
            var orderDate = order.CreationTime;
            var deliveryDate = order.DeliveryDate;
            var pickupDate = order.PickupDate;
            return template
                .Replace("[customer_name]", order.CustomerName ?? "")
                .Replace("[order_number]", order.OrderNumber ?? "")
                .Replace("[order_date]", orderDate.ToString("dd/MM/yyyy"))
                .Replace("[order_total]", (order.Total ?? 0).ToString("N2"))
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
            string? template = null;
            var deliveryType = (order.DeliveryType ?? "").Trim();
            var source = (order.Source ?? "").Trim();
            if (string.Equals(deliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessageShipping;
            else if (string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessagePickup;
            else if (string.Equals(source, "Kiosk", StringComparison.OrdinalIgnoreCase))
                template = settings.OrderReadyCustomerMessageKiosk;
            else
                template = settings.OrderReadyCustomerMessagePickup ?? settings.OrderReadyCustomerMessageShipping;
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
                if (req.DeliveryDate.HasValue) o.DeliveryDate = req.DeliveryDate;
                if (req.DeliveryTime != null) o.DeliveryTime = req.DeliveryTime;
                if (req.PickupDate.HasValue) o.PickupDate = req.PickupDate;
                if (req.PickupTime != null) o.PickupTime = req.PickupTime;
                if (req.DeliveryAddress != null) o.DeliveryAddress = req.DeliveryAddress;
                if (req.PaymentStatus != null) o.PaymentStatus = req.PaymentStatus;
                if (req.BagsCount.HasValue) o.BagsCount = req.BagsCount;
                o.UpdateUserId = AuthUser.Id;
            }, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            // Sync to WooCommerce/oc-storeos when order came from WooCommerce: on any update (status, delivery, notes, etc.). oc-storeos gets full order POST; standard WC gets status PUT.
            if (!string.IsNullOrWhiteSpace(updated.ExternalOrderId) &&
                string.Equals(updated.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase))
            {
                var site = await _siteStorage.GetSiteAsync(updated.SiteId, cancelToken);
                if (site?.WooCommerceEnabled == true)
                {
                    var wcStatus = MapOurStatusToWooCommerce(updated.Status) ?? "on-hold";
                    var orderIdCapture = orderId;
                    var siteIdCapture = updated.SiteId;
                    var externalIdCapture = updated.ExternalOrderId!;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _wooCommerceService.UpdateOrderStatusAsync(siteIdCapture, externalIdCapture, wcStatus, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "WooCommerce/oc-storeos order sync failed for order {OrderId}", orderIdCapture);
                        }
                    }, CancellationToken.None);
                }
            }
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
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
            var order = await _orderStorage.CancelOrderAsync(orderId, AuthUser.Id, softDelete, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            // Sync cancelled status to WooCommerce/oc-storeos only for orders that came from WooCommerce.
            if (!string.IsNullOrWhiteSpace(order.ExternalOrderId) &&
                string.Equals(order.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase))
            {
                var site = await _siteStorage.GetSiteAsync(order.SiteId, cancelToken);
                if (site?.WooCommerceEnabled == true)
                {
                    var siteIdCapture = order.SiteId;
                    var externalIdCapture = order.ExternalOrderId!;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _wooCommerceService.UpdateOrderStatusAsync(siteIdCapture, externalIdCapture, "cancelled", CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "WooCommerce/oc-storeos order cancel sync failed for order {OrderId}", orderId);
                        }
                    }, CancellationToken.None);
                }
            }
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
            if (items.Any(i => (i.ProductId ?? 0) <= 0))
                return CreateResponse(response, StatusCode.InvalidRequest, "Each order item must have a valid ProductId.");

            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "Cannot add items to a cancelled order.");

            var newOrderItems = items.Select(req => _mapper.Map<OrderItem>(req)).ToList();
            var updated = await _orderStorage.AddOrderItemsAsync(orderId, newOrderItems, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
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
            var updated = await _orderStorage.RemoveOrderItemAsync(orderId, orderItemId, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order item not found.");
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
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken);
            response.Data = _mapper.Map<OrderRes>(loaded);
            return response;
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
                        if (isPickup) order.PickupDate = parsed.Value;
                        else order.DeliveryDate = parsed.Value;
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
            }
        }

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

        /// <summary>Resolves WooCommerce order item to our Product.Id: first by WooCommerce product ID (Product.WooCommerceId + site), then by SKU on the site. Returns null if not found (caller may keep payload ProductId as fallback).</summary>
        private async Task<int?> ResolveWooCommerceItemProductIdAsync(int siteId, int accountId, int? wooCommerceProductId, string? sku, CancellationToken cancelToken)
        {
            if (wooCommerceProductId.HasValue && wooCommerceProductId.Value > 0)
            {
                var byWooId = await _productStorage.GetProductIdByWooCommerceIdAndSiteAsync(siteId, wooCommerceProductId.Value, cancelToken).ConfigureAwait(false);
                if (byWooId.HasValue) return byWooId.Value;
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

        /// <summary>For WooCommerce order items: compute quantity, unitWeightGrams and optional variantTitle from product setup so display matches Kiosk/manual (regular=units, by_weight=kg/g, by_unit=units+weight per unit).</summary>
        private static (decimal quantity, decimal? unitWeightGrams, string? variantTitle) GetWooCommerceItemQuantityAndUnitWeight(WooCommerceOrderItemPayload it, Product? product)
        {
            if (product == null)
                return (it.Quantity, null, null);

            var setupTypeName = product.SetupType?.Name ?? "";
            var isWeightedBySetup = setupTypeName is "by_weight" or "by_unit" or "by_unit_and_weight";
            var isWeighted = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetup);

            if (!isWeighted || setupTypeName == "standard")
                return (it.Quantity, null, null);

            // Sold by weight (ק"ג or גרם): WC quantity is weight in kg. Store so display shows weight (e.g. 1.5 ק"ג, 700 גרם).
            if (setupTypeName == "by_weight")
            {
                // Always store weight in kg; frontend shows kg or converts to grams from product unit.
                return (it.Quantity, 1000m, null);
            }

            // by_unit or by_unit_and_weight
            var wc = product.WeightConfig;
            if (wc?.WeightByVariant == true)
            {
                // משקל לפי גודל: use variant weight. Resolve variant by variationId (when sent) or by name (fallback for this site's WooCommerce orders).
                // Woo may send quantity as ordered weight (e.g. 1.6 kg) for by_unit+weightByVariant products.
                // For fractional quantities, convert ordered weight -> units so UI matches kiosk/manual.
                decimal? unitWeightGrams = null;
                var matchedVariant = GetVariantFromPayloadItem(it, product);
                var variantToUse = matchedVariant ?? product.ProductVariant?.FirstOrDefault(v => !v.IsDeleted && v.Weight.HasValue);
                if (variantToUse?.Weight.HasValue == true)
                    unitWeightGrams = (decimal)(variantToUse.Weight!.Value * 1000);
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

            if (!string.IsNullOrWhiteSpace(wc?.UnitWeight) && decimal.TryParse(wc.UnitWeight, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                // מכירה לפי יחידה / בחירת משקל ליחידה:
                // Woo may send quantity as ordered weight (e.g. 0.2 kg) for by_unit products.
                // For fractional quantities, convert ordered weight -> units so UI matches kiosk/manual.
                var unitWeightGrams = string.Equals(wc.Unit?.Name, "g", StringComparison.OrdinalIgnoreCase)
                    ? parsed
                    : parsed * 1000m;
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

            // by_unit_and_weight with no unit weight: treat WC quantity as weight in kg (e.g. 1.4 kg).
            if (setupTypeName == "by_unit_and_weight")
                return (it.Quantity, 1000m, null);

            return (it.Quantity, null, null);
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
            var todayUtc = DateTime.UtcNow.Date;
            if (existing != null)
            {
                var deliveryAddress = payload.ShippingAddress != null
                    ? string.Join(", ", new[] { payload.ShippingAddress.Street, payload.ShippingAddress.City, payload.ShippingAddress.Zip }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : null;
                var updateCustomer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                    siteId, site.AccountId, payload.Customer?.Phone ?? "", payload.Customer?.Name ?? "", email: payload.Customer?.Email,
                    city: null, defaultAddress: deliveryAddress, notes: null, marketingSms: null, cancelToken).ConfigureAwait(false);
                var updated = await _orderStorage.UpdateOrderAsync(existing.Id, o =>
                {
                    o.Status = status;
                    o.CustomerName = payload.Customer?.Name;
                    o.CustomerPhone = payload.Customer?.Phone;
                    o.CustomerEmail = payload.Customer?.Email;
                    o.CustomerId = updateCustomer.Id;
                    o.DeliveryAddress = deliveryAddress;
                    o.CustomerNote = payload.CustomerNotes ?? o.CustomerNote;
                    o.ShippingCost = payload.ShippingTotal;
                    o.Total = payload.OrderTotal ?? o.Total;
                    o.SubTotal = (payload.OrderTotal ?? o.SubTotal) - (payload.ShippingTotal ?? 0);
                    o.UpdatedDate = DateTime.UtcNow;
                    ApplyShippingInfoToOrder(o, payload.ShippingInfo, payload.ShippingLabel);
                    if (!o.DeliveryDate.HasValue) o.DeliveryDate = todayUtc;
                    if (!o.PickupDate.HasValue) o.PickupDate = todayUtc;
                }, cancelToken).ConfigureAwait(false);
                if (updated == null)
                    return CreateResponse(response, StatusCode.ItemNotFound);
                var updateItems = new List<OrderItem>();
                if (payload.Items != null)
                {
                    for (var i = 0; i < payload.Items.Count; i++)
                    {
                        var it = payload.Items[i];
                        var ourProductId = await ResolveWooCommerceItemProductIdAsync(siteId, site.AccountId, it.ProductId, it.Sku, cancelToken).ConfigureAwait(false);
                        Product? product = ourProductId.HasValue ? await _productStorage.GetProductAsync(ourProductId.Value, cancelToken).ConfigureAwait(false) : null;
                        var (qty, unitWeightGrams, variantTitle) = GetWooCommerceItemQuantityAndUnitWeight(it, product);
                        updateItems.Add(new OrderItem
                        {
                            OrderId = existing.Id,
                            ProductId = ourProductId ?? it.ProductId,
                            Title = it.Name,
                            VariantTitle = GetVariantTitleFromPayload(it) ?? variantTitle,
                            Quantity = qty,
                            UnitWeightGrams = unitWeightGrams,
                            PricePerUnit = it.UnitPrice,
                            TotalPrice = it.LineTotal,
                            Notes = !string.IsNullOrWhiteSpace(it.Note) ? it.Note : it.ProductNote,
                            SortOrder = i
                        });
                    }
                }
                await _orderStorage.ReplaceOrderItemsAsync(existing.Id, updateItems, cancelToken).ConfigureAwait(false);
                var loaded = await _orderStorage.GetOrderByIdAsync(existing.Id, cancelToken).ConfigureAwait(false);
                response.Data = _mapper.Map<OrderRes>(loaded!);
                return response;
            }
            var createItems = new List<CreateOrderItemReq>();
            if (payload.Items != null)
            {
                for (var i = 0; i < payload.Items.Count; i++)
                {
                    var it = payload.Items[i];
                    var ourProductId = await ResolveWooCommerceItemProductIdAsync(siteId, site.AccountId, it.ProductId, it.Sku, cancelToken).ConfigureAwait(false);
                    Product? product = ourProductId.HasValue ? await _productStorage.GetProductAsync(ourProductId.Value, cancelToken).ConfigureAwait(false) : null;
                    var (qty, unitWeightGrams, variantTitle) = GetWooCommerceItemQuantityAndUnitWeight(it, product);
                    createItems.Add(new CreateOrderItemReq
                    {
                        ProductId = ourProductId ?? it.ProductId,
                        Title = it.Name,
                        VariantTitle = GetVariantTitleFromPayload(it) ?? variantTitle,
                        Quantity = qty,
                        UnitWeightGrams = unitWeightGrams,
                        PricePerUnit = it.UnitPrice,
                        TotalPrice = it.LineTotal,
                        Notes = !string.IsNullOrWhiteSpace(it.Note) ? it.Note : it.ProductNote,
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
                DeliveryAddress = payload.ShippingAddress != null
                    ? string.Join(", ", new[] { payload.ShippingAddress.Street, payload.ShippingAddress.City, payload.ShippingAddress.Zip }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : null,
                CustomerNote = payload.CustomerNotes,
                ShippingCost = payload.ShippingTotal,
                Total = payload.OrderTotal,
                SubTotal = payload.OrderTotal - (payload.ShippingTotal ?? 0),
                Items = createItems
            };
            var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
                req.SiteId, req.AccountId, req.CustomerPhone, req.CustomerName ?? "", email: req.CustomerEmail,
                city: null, defaultAddress: req.DeliveryAddress, notes: null, marketingSms: null, cancelToken).ConfigureAwait(false);
            var order = _mapper.Map<Order>(req);
            order.CustomerId = customer.Id;
            // Use the date when the order was placed in WooCommerce, not when our API received the webhook
            order.CreationTime = payload.OrderDate.HasValue
                ? (payload.OrderDate.Value.Kind == DateTimeKind.Utc ? payload.OrderDate.Value : payload.OrderDate.Value.ToUniversalTime())
                : DateTime.UtcNow;
            order.CreationUserId = null;
            ApplyShippingInfoToOrder(order, payload.ShippingInfo, payload.ShippingLabel);
            // Default delivery/pickup date to today when not provided (like manual order)
            if (!order.DeliveryDate.HasValue) order.DeliveryDate = todayUtc;
            if (!order.PickupDate.HasValue) order.PickupDate = todayUtc;
            var items = new List<OrderItem>();
            for (var i = 0; i < req.Items.Count; i++)
            {
                var oi = _mapper.Map<OrderItem>(req.Items[i]);
                oi.SortOrder = i;
                items.Add(oi);
            }
            var created = await _orderStorage.CreateOrderAsync(order, items, cancelToken).ConfigureAwait(false);
            var loadedOrder = await _orderStorage.GetOrderByIdAsync(created.Id, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loadedOrder!);
            return response;
        }

        /// <summary>Record payment from WooCommerce (invoice, clearance, paid-at). API key auth.</summary>
        public async Task<IApiResponse<OrderRes>> RecordPaymentFromWooCommerceAsync(int siteId, WooCommerceOrderPaymentPayload payment, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            var order = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, payment.OrderNumber, cancelToken).ConfigureAwait(false);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            var updated = await _orderStorage.UpdateOrderAsync(order.Id, o =>
            {
                if (payment.InvoiceNumber != null) o.InvoiceNumber = payment.InvoiceNumber;
                o.PaymentReference = payment.PaymentReference ?? payment.ClearanceNumber;
                if (payment.PaidAt.HasValue) o.PaidAt = payment.PaidAt;
                o.PaymentStatus = "Paid";
                o.UpdatedDate = DateTime.UtcNow;
            }, cancelToken).ConfigureAwait(false);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            var loaded = await _orderStorage.GetOrderByIdAsync(updated.Id, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loaded!);
            return response;
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
