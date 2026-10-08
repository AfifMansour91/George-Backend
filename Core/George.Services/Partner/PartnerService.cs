using AutoMapper;
using George.Common;
using George.Common.Payment;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services.Partner
{
    /// <summary>
    /// Partner API (/Partner/v1): site-scoped store info, catalog, customer lookup, cart quoting and ordering
    /// for external ordering integrations (the Zano Dagim WhatsApp ordering agent is the first consumer).
    /// The site always comes from the API-key claim, never from the request. Prices, discounts and delivery
    /// fees are always computed server-side from the site's effective catalog and settings so a partner (or an
    /// LLM agent) can never invent them. Contract for integrators: docs/PARTNER_API.md.
    /// </summary>
    public class PartnerService : ServiceBase
    {
        private readonly ProductService _productService;
        private readonly CategoryStorage _categoryStorage;
        private readonly CustomerStorage _customerStorage;
        private readonly OrderService _orderService;
        private readonly OrderStorage _orderStorage;
        private readonly SiteStorage _siteStorage;
        private readonly OrderReceptionStorage _orderReceptionStorage;
        private readonly PromotionService _promotionService;
        private readonly PromotionStorage _promotionStorage;
        private readonly Payments.PaymentService _paymentService;

        private const string DefaultSource = PartnerOrderMapper.SourceWhatsApp;
        private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
            { PartnerOrderMapper.SourceWhatsApp, PartnerOrderMapper.SourcePartner };

        private const string PayCash = "Cash";
        private const string PayPaymentLink = "PaymentLink";
        private const string PaySavedCard = "SavedCard";
        private const string PayOnAccount = "OnAccount";
        private const string PayBankTransfer = "BankTransfer";

        private static readonly string[] HebrewDayNames = { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };

        public PartnerService(
            ILogger<PartnerService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductService productService,
            CategoryStorage categoryStorage,
            CustomerStorage customerStorage,
            OrderService orderService,
            OrderStorage orderStorage,
            SiteStorage siteStorage,
            OrderReceptionStorage orderReceptionStorage,
            PromotionService promotionService,
            PromotionStorage promotionStorage,
            Payments.PaymentService paymentService)
            : base(logger, mapper, cache)
        {
            _productService = productService;
            _categoryStorage = categoryStorage;
            _customerStorage = customerStorage;
            _orderService = orderService;
            _orderStorage = orderStorage;
            _siteStorage = siteStorage;
            _orderReceptionStorage = orderReceptionStorage;
            _promotionService = promotionService;
            _promotionStorage = promotionStorage;
            _paymentService = paymentService;
        }

        //*************************    Site    *************************//

        public async Task<IApiResponse<PartnerSiteRes>> GetSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerSiteRes>();
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            if (site == null)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Site not found.");

            var cities = await _siteStorage.GetCityShippingCostsAsync(siteId, cancelToken).ConfigureAwait(false);
            var reception = await _orderReceptionStorage.GetForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            var account = site.Account;

            response.Data = new PartnerSiteRes
            {
                SiteId = site.Id,
                SiteName = site.SiteName,
                BusinessName = account?.Name,
                Phone = FirstNonEmpty(site.ContactPhone, account?.Phone),
                Email = FirstNonEmpty(site.ContactEmail, account?.ManagerEmail),
                Address = FirstNonEmpty(site.Location, account?.Address),
                City = account?.City,
                Currency = string.IsNullOrWhiteSpace(site.Currency) ? "ILS" : site.Currency,
                IsKosher = site.IsKosherSite ?? account?.IsKosherShop ?? false,
                PickupEnabled = true,
                ShippingEnabled = true,
                ShippingCost = site.ShippingCost ?? 0m,
                FreeShippingAbove = site.FreeShippingAbove is > 0m ? site.FreeShippingAbove : null,
                ShippingCities = cities
                    .OrderBy(c => c.City)
                    .Select(c => new PartnerCityShippingRes { City = c.City, Cost = c.Cost })
                    .ToList(),
                PrepTimeMinutes = site.PrepTimeMinutes,
                PaymentGateway = string.IsNullOrWhiteSpace(site.PaymentGatewayProvider) ? PaymentGatewayProviderId.None : site.PaymentGatewayProvider,
                PaymentMethods = EnabledPaymentMethods(site),
                SupplyDateRequired = true,
                OrderReception = new PartnerOrderReceptionRes
                {
                    TodayDeliveryClosed = reception.TodayDeliveryClosed,
                    TodayPickupClosed = reception.TodayPickupClosed,
                    ClosedDeliveryDates = reception.FutureDeliveryDates,
                    ClosedPickupDates = reception.FuturePickupDates,
                },
                WebhookConfigured = !string.IsNullOrWhiteSpace(site.PartnerWebhookUrl),
                BundlesSupported = false,
                TimeZone = "Asia/Jerusalem",
            };
            return response;
        }

        public async Task<IApiResponse<PartnerAvailabilityRes>> GetAvailabilityAsync(
            int siteId, string? deliveryType, int days, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerAvailabilityRes>();
            var isPickup = IsPickup(deliveryType);
            var isShipping = IsShipping(deliveryType);
            if (!isPickup && !isShipping)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.InvalidDeliveryType, "deliveryType must be Pickup or Shipping.");
            if (days <= 0) days = 14;
            if (days > 60) days = 60;

            var reception = await _orderReceptionStorage.GetForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            var today = TodayLocal();
            var res = new PartnerAvailabilityRes
            {
                DeliveryType = isPickup ? "Pickup" : "Shipping",
                Today = today.ToString("yyyy-MM-dd"),
                SuggestedTimeWindows = isPickup
                    ? new List<string> { "09:00-12:00", "12:00-15:00", "15:00-18:00" }
                    : new List<string> { "10:00-14:00", "14:00-18:00", "18:00-21:00" },
            };
            for (var i = 0; i < days; i++)
            {
                var date = today.AddDays(i);
                var reason = SupplyDateProblem(reception, isPickup, date, today);
                res.Days.Add(new PartnerAvailabilityDayRes
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    DayOfWeek = date.DayOfWeek.ToString(),
                    DayName = HebrewDayNames[(int)date.DayOfWeek],
                    Available = reason == null,
                    Reason = reason?.Message,
                });
            }
            response.Data = res;
            return response;
        }

        //*************************    Catalog    *************************//

        public async Task<IApiResponse<ApiListResponse<PartnerProductRes>>> GetProductsAsync(
            int siteId, string? search, int? categoryId, bool inStockOnly, int skip, int take, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<PartnerProductRes>> { Data = new ApiListResponse<PartnerProductRes>() };

            var listReq = new ApiListReq<ProductFilter>
            {
                Filter = new ProductFilter
                {
                    SiteId = siteId,
                    CategoryId = categoryId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : new SearchFilter { SearchTerm = search },
                    IncludeOptionsAndVariants = true,
                },
                IncludeTotal = true,
            };
            var products = await _productService.GetProductsAsync(listReq, cancelToken).ConfigureAwait(false);
            if (!products.IsSuccessful || products.Data?.Items == null)
                return CreateResponse(response, (StatusCode)products.StatusCode, products.Description);

            // ProductStorage does not page this query; filter and page here.
            var visible = products.Data.Items.Where(IsVisibleToPartner).Select(MapProductToPartner).ToList();
            if (inStockOnly) visible = visible.Where(p => p.InStock).ToList();
            if (take <= 0) take = 50;
            if (take > 500) take = 500;
            if (skip < 0) skip = 0;
            response.Data!.Items = visible.Skip(skip).Take(take).ToList();
            response.Data.Skip = skip;
            response.Data.Limit = take;
            response.Data.Total = visible.Count;
            return response;
        }

        public async Task<IApiResponse<PartnerProductRes>> GetProductAsync(
            int siteId, int accountId, int productId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerProductRes>();
            var product = await LoadPartnerVisibleProductAsync(siteId, accountId, productId, cancelToken).ConfigureAwait(false);
            if (product == null)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.ProductNotFound, "Product not found on this site.");
            response.Data = product;
            return response;
        }

        public async Task<IApiResponse<List<PartnerCategoryRes>>> GetCategoriesAsync(int siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<PartnerCategoryRes>>();
            var res = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId, IsEnabled = true },
                new PagingExDto { IncludeTotal = false, Take = int.MaxValue },
                cancelToken).ConfigureAwait(false);
            response.Data = res.Items
                .OrderBy(c => c.SortOrder ?? int.MaxValue)
                .ThenBy(c => c.Name)
                .Select(c => new PartnerCategoryRes
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentCategoryId = c.ParentCategoryId,
                    Description = c.Description,
                    SortOrder = c.SortOrder,
                    ImageUrl = c.ImageUrl,
                })
                .ToList();
            return response;
        }

        //*************************    Customers    *************************//

        public async Task<IApiResponse<PartnerCustomerRes>> GetCustomerByPhoneAsync(
            int siteId, string? phone, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerCustomerRes> { Data = new PartnerCustomerRes { Found = false } };
            if (string.IsNullOrWhiteSpace(phone))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.PhoneRequired, "phone is required.");

            var customer = await _customerStorage.GetCustomerByPhoneAsync(siteId, phone, cancelToken).ConfigureAwait(false);
            if (customer == null)
                return response;

            var stats = await _customerStorage.GetCustomerGlobalStatsAsync(customer.Id, cancelToken).ConfigureAwait(false);
            var card = await _paymentService.GetSavedCardForCustomerAsync(siteId, customer.Phone ?? phone, customer.Id, cancelToken).ConfigureAwait(false);
            var savedCard = card.IsSuccessful && card.Data?.HasCard == true ? card.Data : null;

            response.Data = new PartnerCustomerRes
            {
                Found = true,
                CustomerId = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone,
                Email = customer.Email,
                City = customer.City,
                DeliveryStreet = customer.DeliveryStreet,
                DeliveryApartment = customer.DeliveryApartment,
                DeliveryFloor = customer.DeliveryFloor,
                DeliveryEntranceCode = customer.DeliveryEntranceCode,
                DefaultAddress = customer.DefaultAddress,
                OrderCount = stats.OrderCount,
                LastOrderDate = stats.LastOrderAt,
                HasSavedCard = savedCard != null,
                SavedCardLast4 = savedCard?.Last4Digits,
                SavedCardBrand = savedCard?.CardBrand,
                MarketingSms = customer.MarketingSms,
            };
            return response;
        }

        /// <summary>Items of the customer's most recent order at this site ("same as last time").</summary>
        public async Task<IApiResponse<List<PartnerOrderItemRes>>> GetLastOrderItemsAsync(
            int siteId, string? phone, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<PartnerOrderItemRes>> { Data = new List<PartnerOrderItemRes>() };
            if (string.IsNullOrWhiteSpace(phone))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.PhoneRequired, "phone is required.");
            var last = await _orderStorage.GetLastOrderByCustomerPhoneAsync(siteId, phone, cancelToken).ConfigureAwait(false);
            if (last == null)
                return response;
            response.Data = (last.OrderItem ?? new List<OrderItem>())
                .Where(i => !i.IsDeleted && !BundleOrderLines.IsBundleChild(i))
                .OrderBy(i => i.SortOrder)
                .Select(PartnerOrderMapper.MapItem)
                .ToList();
            return response;
        }

        /// <summary>The customer's orders at this site, newest first (all sources, so "what about my order?" also covers website orders).</summary>
        public async Task<IApiResponse<ApiListResponse<PartnerOrderRes>>> GetCustomerOrdersAsync(
            int siteId, string? phone, int skip, int take, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<PartnerOrderRes>>
            {
                Data = new ApiListResponse<PartnerOrderRes> { Items = new List<PartnerOrderRes>(), Skip = skip, Limit = take, Total = 0 },
            };
            if (string.IsNullOrWhiteSpace(phone))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.PhoneRequired, "phone is required.");
            if (take <= 0) take = 10;
            if (take > 100) take = 100;
            if (skip < 0) skip = 0;

            var customer = await _customerStorage.GetCustomerByPhoneAsync(siteId, phone, cancelToken).ConfigureAwait(false);
            if (customer == null)
                return response;

            var list = await _orderStorage.GetOrdersAsync(
                new OrderFilter { SiteId = siteId, CustomerId = customer.Id },
                new PagingExDto { Skip = skip, Take = take, IncludeTotal = true },
                cancelToken).ConfigureAwait(false);
            var history = await _orderStorage
                .GetStatusHistoryByOrderIdsAsync(list.Items.Select(o => o.Id).ToList(), cancelToken)
                .ConfigureAwait(false);
            response.Data!.Items = list.Items
                .Select(o => PartnerOrderMapper.Map(o, history.GetValueOrDefault(o.Id), alreadyExisted: false))
                .ToList();
            response.Data.Skip = skip;
            response.Data.Limit = take;
            response.Data.Total = list.Total;
            return response;
        }

        //*************************    Quote    *************************//

        public async Task<IApiResponse<PartnerQuoteRes>> QuoteAsync(
            int siteId, int accountId, PartnerQuoteReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerQuoteRes>();
            var deliveryType = string.IsNullOrWhiteSpace(req.DeliveryType) ? "Pickup" : req.DeliveryType.Trim();
            var isPickup = IsPickup(deliveryType);
            var isShipping = IsShipping(deliveryType);
            if (!isPickup && !isShipping)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.InvalidDeliveryType, "DeliveryType must be Pickup or Shipping.");

            var quote = await BuildQuoteAsync(siteId, accountId, req.Items, isShipping, req.DeliveryCity, req.CouponCode, req.CustomerPhone, cancelToken)
                .ConfigureAwait(false);

            // Optional supply-date check (same rule as create).
            var date = isPickup ? req.PickupDate : req.DeliveryDate;
            if (date.HasValue)
            {
                var reception = await _orderReceptionStorage.GetForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                var problem = SupplyDateProblem(reception, isPickup, date.Value, TodayLocal());
                if (problem != null)
                    quote.Errors.Add(new PartnerQuoteErrorRes { Code = problem.Value.Code, Message = problem.Value.Message });
            }
            quote.IsValid = quote.Errors.Count == 0;
            response.Data = quote;
            return response;
        }

        //*************************    Orders    *************************//

        public async Task<IApiResponse<PartnerOrderRes>> CreateOrderAsync(
            int siteId, int accountId, PartnerCreateOrderReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerOrderRes>();

            if (req.Items == null || req.Items.Count == 0)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.EmptyCart, "At least one order item is required.");
            if (string.IsNullOrWhiteSpace(req.CustomerName))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.CustomerRequired, "CustomerName is required.");
            if (string.IsNullOrWhiteSpace(req.CustomerPhone))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.PhoneRequired, "CustomerPhone is required.");
            var isPickup = IsPickup(req.DeliveryType);
            var isShipping = IsShipping(req.DeliveryType);
            if (!isPickup && !isShipping)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.InvalidDeliveryType, "DeliveryType must be Pickup or Shipping.");
            if (isPickup && !req.PickupDate.HasValue)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.SupplyDateRequired, "PickupDate is required for pickup orders.");
            if (isShipping && !req.DeliveryDate.HasValue)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.SupplyDateRequired, "DeliveryDate is required for shipping orders.");
            if (isShipping && (string.IsNullOrWhiteSpace(req.DeliveryStreet) || string.IsNullOrWhiteSpace(req.DeliveryCity)))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.AddressRequired, "DeliveryStreet and DeliveryCity are required for shipping orders.");

            var source = string.IsNullOrWhiteSpace(req.Source) ? DefaultSource : req.Source.Trim();
            if (!AllowedSources.Contains(source))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.InvalidSource, "Source must be WhatsApp or Partner.");

            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            if (site == null)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Site not found.");

            // Payment method: must be one the site enables; SavedCard additionally needs a card on file.
            var payCode = string.IsNullOrWhiteSpace(req.PaymentMethod) ? PayCash : req.PaymentMethod.Trim();
            var allowed = EnabledPaymentMethods(site);
            var chosen = allowed.FirstOrDefault(m => string.Equals(m.Code, payCode, StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.PaymentMethodNotAllowed,
                    $"PaymentMethod '{payCode}' is not enabled on this site. Allowed: {string.Join(", ", allowed.Select(m => m.Code))}.");
            int? savedCardPmId = null;
            if (string.Equals(chosen.Code, PaySavedCard, StringComparison.OrdinalIgnoreCase))
            {
                var card = await _paymentService.GetSavedCardForCustomerAsync(siteId, req.CustomerPhone, null, cancelToken).ConfigureAwait(false);
                if (!card.IsSuccessful || card.Data?.HasCard != true)
                    return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.SavedCardMissing, "The customer has no saved card at this site. Use PaymentLink instead.");
                savedCardPmId = card.Data.CustomerPaymentMethodId;
            }

            // Idempotency: same PartnerRef at this site returns the existing order (safe agent retries).
            var partnerRef = string.IsNullOrWhiteSpace(req.PartnerRef) ? null : req.PartnerRef.Trim();
            if (partnerRef != null)
            {
                var existing = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, partnerRef, cancelToken).ConfigureAwait(false);
                if (existing != null)
                    return await BuildPartnerOrderResponseAsync(existing.Id, alreadyExisted: true, cancelToken).ConfigureAwait(false);
            }

            // Supply date must be acceptable (not in the past, shop not closed that day).
            var reception = await _orderReceptionStorage.GetForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
            var supplyDate = isPickup ? req.PickupDate!.Value : req.DeliveryDate!.Value;
            var dateProblem = SupplyDateProblem(reception, isPickup, supplyDate, TodayLocal());
            if (dateProblem != null)
                return Fail(response, StatusCode.InvalidRequest, dateProblem.Value.Code, dateProblem.Value.Message);

            // Price the cart exactly like the quote endpoint does.
            var quote = await BuildQuoteAsync(siteId, accountId, req.Items, isShipping, req.DeliveryCity, req.CouponCode, req.CustomerPhone, cancelToken)
                .ConfigureAwait(false);
            if (quote.Errors.Count > 0)
            {
                var first = quote.Errors[0];
                var prefix = first.Line.HasValue ? $"Line {first.Line}: " : "";
                return Fail(response, StatusCode.InvalidRequest, first.Code, prefix + first.Message);
            }

            var lines = quote.Lines.Select(l => new CreateOrderItemReq
            {
                ProductId = l.ProductId,
                ProductVariantId = l.ProductVariantId,
                Title = l.Title,
                VariantTitle = l.VariantTitle,
                Quantity = l.Quantity,
                PricePerUnit = l.UnitPrice,
                TotalPrice = l.LineTotal,
                Notes = l.Notes,
                SortOrder = l.Line - 1,
            }).ToList();

            var createReq = new CreateOrderReq
            {
                SiteId = siteId,
                AccountId = accountId,
                Source = source,
                Status = "New",
                PaymentStatus = "Unpaid",
                PaymentMethod = ToOrderPaymentMethod(chosen.Code),
                CustomerPaymentMethodId = savedCardPmId,
                DeliveryType = isPickup ? "Pickup" : "Shipping",
                CustomerName = req.CustomerName.Trim(),
                CustomerPhone = req.CustomerPhone.Trim(),
                CustomerEmail = string.IsNullOrWhiteSpace(req.CustomerEmail) ? null : req.CustomerEmail.Trim(),
                MarketingSms = req.MarketingSms,
                PickupDate = req.PickupDate,
                PickupTime = req.PickupTime,
                DeliveryDate = req.DeliveryDate,
                DeliveryTime = req.DeliveryTime,
                DeliveryStreet = req.DeliveryStreet,
                DeliveryCity = req.DeliveryCity,
                DeliveryApartment = req.DeliveryApartment,
                DeliveryFloor = req.DeliveryFloor,
                DeliveryEntranceCode = req.DeliveryEntranceCode,
                CustomerNote = req.CustomerNote,
                CouponCode = quote.CouponApplied ? quote.CouponCode : null,
                ExternalOrderId = partnerRef,
                SubTotal = quote.SubTotal,
                ShippingCost = quote.ShippingCost,
                Total = quote.Total,
                Items = lines,
            };

            var created = await _orderService.CreateOrderAsync(createReq, cancelToken).ConfigureAwait(false);
            if (!created.IsSuccessful || created.Data == null)
                return CreateResponse(response, (StatusCode)created.StatusCode, created.Description);
            return await BuildPartnerOrderResponseAsync(created.Data.Id, alreadyExisted: false, cancelToken).ConfigureAwait(false);
        }

        public async Task<IApiResponse<PartnerOrderRes>> GetOrderAsync(int siteId, int orderId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerOrderRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null || order.SiteId != siteId)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Order not found.");
            return await BuildPartnerOrderResponseAsync(orderId, alreadyExisted: false, cancelToken).ConfigureAwait(false);
        }

        public async Task<IApiResponse<PartnerOrderRes>> GetOrderByRefAsync(int siteId, string partnerRef, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerOrderRes>();
            if (string.IsNullOrWhiteSpace(partnerRef))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.InvalidRequest, "partnerRef is required.");
            var order = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, partnerRef.Trim(), cancelToken).ConfigureAwait(false);
            if (order == null)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Order not found.");
            return await BuildPartnerOrderResponseAsync(order.Id, alreadyExisted: false, cancelToken).ConfigureAwait(false);
        }

        /// <summary>Cancel an order the partner placed while it is still New (not yet in picking). Restores stock, voids any hold.</summary>
        public async Task<IApiResponse<PartnerOrderRes>> CancelOrderAsync(int siteId, int orderId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerOrderRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null || order.SiteId != siteId)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Order not found.");
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return await BuildPartnerOrderResponseAsync(orderId, alreadyExisted: false, cancelToken).ConfigureAwait(false);
            if (!PartnerOrderMapper.IsPartnerSource(order.Source))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.OrderNotCancellable, "Only orders placed through the Partner API can be cancelled here.");
            if (!string.Equals(order.Status, "New", StringComparison.OrdinalIgnoreCase))
                return Fail(response, StatusCode.InvalidRequest, PartnerErrorCode.OrderNotCancellable,
                    $"Order is already {order.Status}; ask the shop to cancel it.");

            var cancelled = await _orderService.CancelOrderAsync(orderId, softDelete: false, cancelToken).ConfigureAwait(false);
            if (!cancelled.IsSuccessful)
                return CreateResponse(response, (StatusCode)cancelled.StatusCode, cancelled.Description);
            return await BuildPartnerOrderResponseAsync(orderId, alreadyExisted: false, cancelToken).ConfigureAwait(false);
        }

        /// <summary>Create a hosted payment page session for the order and return its URL (site must have a payment gateway configured).</summary>
        public async Task<IApiResponse<PartnerPaymentLinkRes>> CreatePaymentLinkAsync(int siteId, int orderId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerPaymentLinkRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null || order.SiteId != siteId)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Order not found.");
            if (order.PaymentSettleStatus is PaymentSettleStatus.Authorized or PaymentSettleStatus.Captured
                || string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                response.Data = new PartnerPaymentLinkRes { OrderId = orderId, PaymentUrl = null, AlreadyPaid = true };
                return response;
            }
            var session = await _paymentService.CreatePaymentSessionAsync(orderId, channel: null, cancelToken).ConfigureAwait(false);
            if (!session.IsSuccessful || session.Data == null)
            {
                var code = (session.Description ?? "").Contains("gateway", StringComparison.OrdinalIgnoreCase)
                    ? PartnerErrorCode.GatewayNotConfigured
                    : PartnerErrorCode.InvalidRequest;
                return Fail(response, (StatusCode)session.StatusCode, code, session.Description ?? "Could not create a payment link.");
            }
            response.Data = new PartnerPaymentLinkRes
            {
                OrderId = orderId,
                PaymentUrl = session.Data.PaymentUrl,
                AlreadyPaid = session.Data.PaymentUrl == null,
            };
            return response;
        }

        //*************************    Pricing core    *************************//

        /// <summary>
        /// Resolve every line against the site's effective catalog, price it, run the promotion engine and the
        /// delivery-fee rules. Shared by Quote and CreateOrder so the customer never sees a total that differs
        /// from what gets ordered.
        /// </summary>
        private async Task<PartnerQuoteRes> BuildQuoteAsync(
            int siteId, int accountId, List<PartnerOrderItemReq>? items, bool isShipping, string? deliveryCity,
            string? couponCode, string? customerPhone, CancellationToken cancelToken)
        {
            var quote = new PartnerQuoteRes { CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim() };
            if (items == null || items.Count == 0)
            {
                quote.Errors.Add(new PartnerQuoteErrorRes { Code = PartnerErrorCode.EmptyCart, Message = "At least one item is required." });
                return quote;
            }

            var productCache = new Dictionary<int, PartnerProductRes?>();
            var productCategoryIds = new Dictionary<int, List<int>>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var lineNo = i + 1;
                if (item.Quantity <= 0m)
                {
                    quote.Errors.Add(Err(lineNo, PartnerErrorCode.InvalidQuantity, "Quantity must be positive."));
                    continue;
                }
                if (!productCache.TryGetValue(item.ProductId, out var product))
                {
                    product = await LoadPartnerVisibleProductAsync(siteId, accountId, item.ProductId, cancelToken).ConfigureAwait(false);
                    productCache[item.ProductId] = product;
                }
                if (product == null)
                {
                    quote.Errors.Add(Err(lineNo, PartnerErrorCode.ProductNotFound, $"Product {item.ProductId} is not available on this site."));
                    continue;
                }
                productCategoryIds[product.Id] = product.CategoryIds;

                PartnerProductVariantRes? variant = null;
                if (item.ProductVariantId is int variantId)
                {
                    variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
                    if (variant == null)
                    {
                        quote.Errors.Add(Err(lineNo, PartnerErrorCode.VariantNotFound, $"Variant {variantId} does not belong to product {item.ProductId}."));
                        continue;
                    }
                }
                else if (product.RequiresVariant)
                {
                    quote.Errors.Add(Err(lineNo, PartnerErrorCode.VariantRequired,
                        $"Product {item.ProductId} requires a ProductVariantId. Options: {string.Join(" | ", product.Variants.Select(v => $"{v.Id}={v.Title}"))}."));
                    continue;
                }

                if (!product.InStock || (variant != null && !variant.InStock))
                {
                    quote.Errors.Add(Err(lineNo, PartnerErrorCode.OutOfStock, $"{product.Name}{(variant != null ? " / " + variant.Title : "")} is out of stock."));
                    continue;
                }

                var unitPrice = variant?.EffectivePrice ?? product.EffectivePrice;
                if (unitPrice is not > 0m)
                {
                    quote.Errors.Add(Err(lineNo, PartnerErrorCode.NoPrice, $"Product {item.ProductId} has no price at this site."));
                    continue;
                }

                var lineTotal = Math.Round(unitPrice.Value * item.Quantity, 2, MidpointRounding.AwayFromZero);
                quote.Lines.Add(new PartnerQuoteLineRes
                {
                    Line = lineNo,
                    ProductId = product.Id,
                    ProductVariantId = variant?.Id,
                    Title = product.Name,
                    VariantTitle = variant?.Title,
                    Quantity = item.Quantity,
                    SoldBy = product.SoldBy,
                    UnitPrice = unitPrice.Value,
                    LineTotal = lineTotal,
                    Notes = item.Notes,
                });
            }

            quote.SubTotal = Math.Round(quote.Lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
            quote.HasWeightLines = quote.Lines.Any(l => l.SoldBy == "weight");

            if (quote.Lines.Count > 0)
                await ApplyPromotionsToQuoteAsync(siteId, quote, productCategoryIds, customerPhone, cancelToken).ConfigureAwait(false);

            if (isShipping && quote.Lines.Count > 0)
            {
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                var cities = await _siteStorage.GetCityShippingCostsAsync(siteId, cancelToken).ConfigureAwait(false);
                var (fee, free) = ResolveShippingFee(site, cities, deliveryCity, quote.SubTotal);
                quote.ShippingCost = fee;
                quote.FreeShippingApplied = free;
            }

            quote.Total = Math.Max(0m, Math.Round(quote.SubTotal - quote.DiscountTotal + quote.ShippingCost, 2, MidpointRounding.AwayFromZero));
            quote.IsValid = quote.Errors.Count == 0;
            return quote;
        }

        /// <summary>Same engine and channel mapping the order-creation path uses (Source WhatsApp/Partner → channel "web").</summary>
        private async Task ApplyPromotionsToQuoteAsync(
            int siteId, PartnerQuoteRes quote, Dictionary<int, List<int>> productCategoryIds, string? customerPhone, CancellationToken cancelToken)
        {
            try
            {
                var evalReq = new EvaluatePromotionsReq
                {
                    SiteId = siteId,
                    Channel = "web",
                    CouponCode = quote.CouponCode,
                    CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim(),
                    CartTotal = quote.SubTotal,
                    Cart = quote.Lines.Select(l =>
                    {
                        productCategoryIds.TryGetValue(l.ProductId, out var cats);
                        var catStrings = (cats ?? new List<int>()).Select(c => c.ToString()).ToList();
                        return new EvaluateCartLine
                        {
                            ProductId = l.ProductId.ToString(),
                            Quantity = l.Quantity,
                            PricePerUnit = l.UnitPrice,
                            CategoryId = catStrings.FirstOrDefault(),
                            CategoryIds = catStrings.Count > 0 ? catStrings : null,
                            Unit = l.SoldBy == "weight" ? "kg" : "unit",
                        };
                    }).ToList(),
                };
                var eval = await _promotionService.EvaluatePromotionsAsync(evalReq, cancelToken).ConfigureAwait(false);
                if (!eval.IsSuccessful || eval.Data == null) return;

                foreach (var applied in eval.Data.PromotionsApplied)
                {
                    quote.PromotionsApplied.Add(new PartnerAppliedPromotionRes
                    {
                        PromotionId = applied.PromotionId,
                        Name = applied.PromotionName,
                        Type = applied.PromotionType,
                        DiscountAmount = applied.DiscountAmount,
                    });
                    if (applied.EligibleItems is { Count: > 0 })
                    {
                        foreach (var ei in applied.EligibleItems)
                            StampLine(quote, ei.ProductId, ei.DiscountAmount, applied.PromotionName);
                    }
                    else if (applied.DiscountAmount > 0m)
                    {
                        var target = applied.RewardProductId?.ToString()
                            ?? applied.TriggerProductIds?.FirstOrDefault()
                            ?? quote.Lines[0].ProductId.ToString();
                        StampLine(quote, target, applied.DiscountAmount, applied.PromotionName);
                    }
                }
                quote.DiscountTotal = Math.Min(quote.SubTotal, Math.Round(eval.Data.TotalDiscount, 2, MidpointRounding.AwayFromZero));
                quote.PromotionsNearby = eval.Data.PromotionsNearby
                    .Select(n => new PartnerNearbyPromotionRes { PromotionId = n.PromotionId, Name = n.PromotionName, PotentialSaving = n.PotentialSaving })
                    .ToList();

                if (quote.CouponCode != null)
                {
                    var active = await _promotionStorage.GetActivePromotionsForEvaluationAsync(siteId, DateTime.UtcNow, cancelToken).ConfigureAwait(false);
                    var couponPromoIds = active
                        .Where(p => !string.IsNullOrWhiteSpace(p.CouponCode)
                            && string.Equals(p.CouponCode.Trim(), quote.CouponCode, StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.Id)
                        .ToHashSet();
                    quote.CouponApplied = couponPromoIds.Count > 0 && eval.Data.PromotionsApplied.Any(a => couponPromoIds.Contains(a.PromotionId));
                    if (!quote.CouponApplied)
                        quote.CouponMessage = couponPromoIds.Count == 0
                            ? "קוד הקופון לא קיים או לא בתוקף."
                            : "הסל לא עומד בתנאי הקופון.";
                }
            }
            catch (Exception ex)
            {
                // Promotions must never block a quote/order (same policy as OrderService).
                _logger.LogError(ex, "Partner quote promotion evaluation failed siteId={SiteId}", siteId);
            }
        }

        private static void StampLine(PartnerQuoteRes quote, string productId, decimal amount, string? promotionName)
        {
            if (amount <= 0m) return;
            var line = quote.Lines.FirstOrDefault(l => l.ProductId.ToString() == productId && l.DiscountAmount == 0m)
                ?? quote.Lines.FirstOrDefault(l => l.ProductId.ToString() == productId);
            if (line == null) return;
            line.DiscountAmount = Math.Round(line.DiscountAmount + amount, 2, MidpointRounding.AwayFromZero);
            line.PromotionName ??= promotionName;
        }

        /// <summary>Delivery fee: free above the site threshold (subtotal before discounts, like the shop UI), else the city fee, else the default fee.</summary>
        public static (decimal Fee, bool Free) ResolveShippingFee(Site? site, IReadOnlyList<SiteCityShippingCost> cities, string? city, decimal subTotal)
        {
            if (site?.FreeShippingAbove is > 0m && subTotal >= site.FreeShippingAbove.Value)
                return (0m, true);
            var c = city?.Trim();
            if (!string.IsNullOrEmpty(c))
            {
                var match = cities.FirstOrDefault(x => string.Equals(x.City?.Trim(), c, StringComparison.OrdinalIgnoreCase));
                if (match != null) return (match.Cost, false);
            }
            return (site?.ShippingCost ?? 0m, false);
        }

        //*************************    Private Methods    *************************//

        private static List<PartnerPaymentMethodRes> EnabledPaymentMethods(Site site)
        {
            var gateway = string.IsNullOrWhiteSpace(site.PaymentGatewayProvider) ? PaymentGatewayProviderId.None : site.PaymentGatewayProvider.Trim().ToLowerInvariant();
            var hasGateway = gateway != PaymentGatewayProviderId.None;
            var list = new List<PartnerPaymentMethodRes>();
            if (site.PaymentCashEnabled ?? true)
                list.Add(new PartnerPaymentMethodRes { Code = PayCash, Name = "מזומן", Description = "תשלום בעת האיסוף / המסירה." });
            if (hasGateway && (site.PaymentCreditSmsEnabled ?? true))
                list.Add(new PartnerPaymentMethodRes { Code = PayPaymentLink, Name = "אשראי בקישור", Description = "צור את ההזמנה ואז POST Orders/{id}/PaymentLink ושלח ללקוח את הקישור." });
            if (hasGateway && (gateway != PaymentGatewayProviderId.Cardcom || site.CardcomSaveCardEnabled))
                list.Add(new PartnerPaymentMethodRes { Code = PaySavedCard, Name = "כרטיס שמור", Description = "רק כאשר Customer.HasSavedCard=true; הכרטיס נתפס/מחויב אוטומטית." });
            if (site.PaymentOnAccountEnabled == true)
                list.Add(new PartnerPaymentMethodRes { Code = PayOnAccount, Name = "בהקפה", Description = "לקוחות עסקיים בהסדר." });
            if (site.PaymentBankTransferEnabled == true)
                list.Add(new PartnerPaymentMethodRes { Code = PayBankTransfer, Name = "העברה בנקאית", Description = null });
            return list;
        }

        private static string ToOrderPaymentMethod(string code) => code switch
        {
            PayPaymentLink => "CreditSms",
            PaySavedCard => "SavedCard",
            PayOnAccount => "OnAccount",
            PayBankTransfer => "BankTransfer",
            _ => "Cash",
        };

        private static bool IsPickup(string? t) => string.Equals(t?.Trim(), "Pickup", StringComparison.OrdinalIgnoreCase);
        private static bool IsShipping(string? t) => string.Equals(t?.Trim(), "Shipping", StringComparison.OrdinalIgnoreCase);

        public static (string Code, string Message)? SupplyDateProblem(OrderReceptionData reception, bool isPickup, DateOnly date, DateOnly today)
        {
            if (date < today)
                return (PartnerErrorCode.SupplyDateInPast, "תאריך האספקה כבר עבר.");
            var kind = isPickup ? "איסוף" : "משלוח";
            if (date == today && (isPickup ? reception.TodayPickupClosed : reception.TodayDeliveryClosed))
                return (PartnerErrorCode.ShopClosed, $"החנות לא מקבלת הזמנות {kind} להיום.");
            var closed = isPickup ? reception.FuturePickupDates : reception.FutureDeliveryDates;
            if (closed.Contains(date.ToString("yyyy-MM-dd")))
                return (PartnerErrorCode.ShopClosed, $"החנות לא מקבלת הזמנות {kind} בתאריך {date:dd/MM/yyyy}.");
            return null;
        }

        private static DateOnly TodayLocal()
        {
            try
            {
                TimeZoneInfo tz;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time"); }
                catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem"); }
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            }
            catch
            {
                return DateOnly.FromDateTime(DateTime.UtcNow);
            }
        }

        private static PartnerQuoteErrorRes Err(int line, string code, string message) =>
            new() { Line = line, Code = code, Message = message };

        private IApiResponse<T> Fail<T>(IApiResponse<T> response, StatusCode status, string code, string message) =>
            CreateResponse(response, status, PartnerErrorCode.Format(code, message));

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        /// <summary>Load a product with the site's effective values, or null when it does not exist, belongs to another account, is not on the site, or is hidden.</summary>
        private async Task<PartnerProductRes?> LoadPartnerVisibleProductAsync(
            int siteId, int accountId, int productId, CancellationToken cancelToken)
        {
            var res = await _productService.GetProductAsync(productId, cancelToken, siteId).ConfigureAwait(false);
            var product = res.IsSuccessful ? res.Data : null;
            if (product == null) return null;
            if (product.AccountId != accountId) return null;
            if (product.SiteIds is { Count: > 0 } siteIds && !siteIds.Contains(siteId)) return null;
            return IsVisibleToPartner(product) ? MapProductToPartner(product) : null;
        }

        private static bool IsVisibleToPartner(ProductRes p)
        {
            if (p.IsExcludedForSite == true) return false;
            if (string.Equals(p.Status, "hidden", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(p.Status, "draft", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(p.Status, "archived", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(p.Visibility, "hidden", StringComparison.OrdinalIgnoreCase)) return false;
            // Bundles (מארזים) need slot selection the Partner API v1 does not model - omitted from the catalog.
            if (string.Equals(p.SetupType, "bundle", StringComparison.OrdinalIgnoreCase) || p.Bundle != null) return false;
            return true;
        }

        private static bool IsSaleActive(decimal? salePrice, DateTime? start, DateTime? end)
        {
            if (salePrice is not > 0m) return false;
            var now = DateTime.UtcNow;
            if (start.HasValue && now < start.Value) return false;
            if (end.HasValue && now > end.Value) return false;
            return true;
        }

        private static PartnerProductRes MapProductToPartner(ProductRes p)
        {
            var saleActive = IsSaleActive(p.SalePrice, p.SalePriceStartDate, p.SalePriceEndDate);
            var soldByWeight = string.Equals(p.SetupType, "by_weight", StringComparison.OrdinalIgnoreCase) || p.IsWeighted == true;
            var stockByQuantity = string.Equals(p.StockManagementType, "quantity", StringComparison.OrdinalIgnoreCase);
            var stockByVariation = string.Equals(p.StockManagementType, "variation", StringComparison.OrdinalIgnoreCase);
            // Status-managed products keep stale StockQuantity residues (99,999,999 on some sites) - only a
            // quantity-managed product is judged by its quantity.
            var inStock = !string.Equals(p.Status, "outOfStock", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p.StockStatus, "out_of_stock", StringComparison.OrdinalIgnoreCase)
                && !(stockByQuantity && p.StockQuantity <= 0m);
            var variants = (p.Variants ?? new List<ProductVariantRes>())
                .Select(v =>
                {
                    var variantSale = v.SalePrice is > 0m ? v.SalePrice : null;
                    var basePrice = v.Price ?? (saleActive ? p.SalePrice : p.Price);
                    // Variant stock counts only when stock is tracked per variation by quantity; otherwise the
                    // product-level flag decides (variant rows carry residues too).
                    var variantInStock = inStock
                        && !(stockByVariation && p.VariationStockByQuantity == true && v.StockQuantity is <= 0m);
                    return new PartnerProductVariantRes
                    {
                        Id = v.Id,
                        Title = v.OptionValues == null ? null : string.Join(" / ", v.OptionValues.Values),
                        OptionValues = v.OptionValues,
                        Price = v.Price,
                        SalePrice = variantSale,
                        EffectivePrice = variantSale ?? basePrice,
                        Sku = v.Sku,
                        InStock = variantInStock,
                    };
                })
                .ToList();
            if (variants.Count > 0 && inStock && variants.All(v => !v.InStock))
                inStock = false;

            return new PartnerProductRes
            {
                Id = p.Id,
                Name = p.Name,
                ShortDescription = p.ShortDescription,
                Sku = p.Sku,
                Price = p.Price,
                SalePrice = saleActive ? p.SalePrice : null,
                EffectivePrice = saleActive ? p.SalePrice : p.Price,
                SoldBy = soldByWeight ? "weight" : "unit",
                WeightUnit = p.WeightUnit,
                ApproxUnitWeightGrams = ApproxUnitWeightGrams(p),
                InStock = inStock,
                RequiresVariant = variants.Count > 0,
                CategoryIds = (p.CategoryIds ?? new List<int>()).Concat(p.SubcategoryIds ?? new List<int>()).Distinct().ToList(),
                Tags = p.Tags ?? new List<string>(),
                ImageUrl = p.ImageUrls?.FirstOrDefault(),
                IsKosher = p.IsKosher,
                Options = (p.ProductOptions ?? new List<ProductOptionRes>())
                    .Select(o => new PartnerProductOptionRes { Name = o.Name, Values = o.Values ?? new List<string>() })
                    .ToList(),
                Variants = variants,
            };
        }

        /// <summary>Unit weight of a unit-sold product in grams when the catalog carries one (e.g. WeightConfig.UnitWeight "0.7" kg or "700" g).</summary>
        private static decimal? ApproxUnitWeightGrams(ProductRes p)
        {
            var raw = p.WeightConfig?.UnitWeight;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!decimal.TryParse(raw.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0m)
                return null;
            var unit = (p.WeightConfig?.Unit ?? p.WeightUnit ?? "kg").Trim().ToLowerInvariant();
            return unit == "g" || unit == "ml" ? value : value * 1000m;
        }

        private async Task<IApiResponse<PartnerOrderRes>> BuildPartnerOrderResponseAsync(
            int orderId, bool alreadyExisted, CancellationToken cancelToken)
        {
            var response = new ApiResponse<PartnerOrderRes>();
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null)
                return Fail(response, StatusCode.ItemNotFound, PartnerErrorCode.OrderNotFound, "Order not found.");
            var history = await _orderStorage.GetStatusHistoryByOrderIdsAsync(new[] { orderId }, cancelToken).ConfigureAwait(false);
            response.Data = PartnerOrderMapper.Map(order, history.GetValueOrDefault(orderId), alreadyExisted);
            return response;
        }
    }
}
