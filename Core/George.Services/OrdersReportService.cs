using System.Globalization;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace George.Services
{
    /// <summary>
    /// דוח הזמנות - operational orders/deliveries view for a supply-date (or order-date) window:
    /// KPI split of shipping vs pickup with sent/pending counts, and a courier-friendly row per order
    /// (address, phone, time window, payment kind, notes).
    /// </summary>
    public class OrdersReportService : ServiceBase
    {
        private readonly OrdersReportStorage _storage;

        public OrdersReportService(
            ILogger<OrdersReportService> logger,
            IMapper mapper,
            CacheManager cache,
            OrdersReportStorage storage)
            : base(logger, mapper, cache)
        {
            _storage = storage;
        }

        public async Task<IApiResponse<OrdersReportRes>> GetReportAsync(
            int siteId,
            DateTime? from,
            DateTime? to,
            string? dateBasis,
            string? fulfillment,
            string? deliveryType,
            string? paymentKind,
            string? cities,
            string? search,
            CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrdersReportRes>();
            if (siteId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

            // Default window: today (Israel calendar) - the field spec's default view.
            var todayLocal = IsraelToday();
            var fromD = (from ?? todayLocal).Date;
            var toD = (to ?? fromD).Date;
            if (toD < fromD)
                return CreateResponse(response, StatusCode.InvalidRequest, "Invalid date range.");

            var byOrderDate = string.Equals(dateBasis?.Trim(), "order", StringComparison.OrdinalIgnoreCase);

            var orders = await _storage
                .GetOrdersForReportAsync(siteId, fromD, toD, byOrderDate, cancelToken)
                .ConfigureAwait(false);

            // City options come from the whole date window, before any other filter (revenue-report pattern).
            var cityNames = orders
                .Select(o => o.DeliveryCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var hasCityNone = orders.Any(o => string.IsNullOrWhiteSpace(o.DeliveryCity));

            var filtered = ApplyFilters(orders, deliveryType, paymentKind, cities, search);

            // KPIs deliberately ignore the fulfillment filter so the sent/pending split always shows both sides.
            var kpis = BuildKpis(filtered);

            var fulfillmentMode = (fulfillment ?? "all").Trim().ToLowerInvariant();
            IEnumerable<Order> visible = fulfillmentMode switch
            {
                "supplied" => filtered.Where(IsFulfilled),
                "notsupplied" => filtered.Where(o => !IsFulfilled(o)),
                _ => filtered,
            };

            var rows = visible
                .Select(BuildRow)
                .OrderBy(r => r.SupplyDateLocal, StringComparer.Ordinal)
                .ThenBy(r => SupplyTimeSortKey(r.SupplyTime))
                .ThenBy(r => r.City ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.OrderNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var res = new OrdersReportRes
            {
                Range = new OrdersReportRangeDto
                {
                    FromLocal = fromD.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ToLocal = toD.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                },
                Kpis = kpis,
                Cities = cityNames,
                HasCityNone = hasCityNone,
                Rows = rows,
            };

            response.Data = res;
            return response;
        }

        private static List<Order> ApplyFilters(
            List<Order> orders,
            string? deliveryType,
            string? paymentKind,
            string? cities,
            string? search)
        {
            IEnumerable<Order> q = orders;

            var dt = (deliveryType ?? "all").Trim().ToLowerInvariant();
            if (dt == "shipping")
                q = q.Where(IsShipping);
            else if (dt == "pickup")
                q = q.Where(o => !IsShipping(o));

            var pay = (paymentKind ?? "all").Trim().ToLowerInvariant();
            if (pay == "cash" || pay == "credit")
                q = q.Where(o => ResolvePaymentKind(o) == pay);

            var cityKeys = (cities ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (cityKeys.Count > 0)
            {
                var includeNoCity = cityKeys.Contains(OrderStorage.CityNoneKey);
                var named = cityKeys
                    .Where(c => c != OrderStorage.CityNoneKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                q = q.Where(o =>
                    string.IsNullOrWhiteSpace(o.DeliveryCity)
                        ? includeNoCity
                        : named.Contains(o.DeliveryCity.Trim()));
            }

            var term = search?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(o =>
                    Contains(o.OrderNumber, term) ||
                    Contains(o.ExternalOrderId, term) ||
                    Contains(o.CustomerName, term) ||
                    Contains(o.DeliveryRecipientName, term) ||
                    Contains(o.CustomerPhone, term) ||
                    Contains(o.DeliveryRecipientPhone, term) ||
                    Contains(o.DeliveryCity, term) ||
                    Contains(o.DeliveryStreet, term) ||
                    Contains(o.DeliveryAddress, term));
            }

            return q.ToList();
        }

        private static OrdersReportKpisDto BuildKpis(List<Order> orders)
        {
            var kpis = new OrdersReportKpisDto
            {
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.Total ?? 0m),
            };

            foreach (var o in orders)
            {
                var fulfilled = IsFulfilled(o);
                if (IsShipping(o))
                {
                    kpis.DeliveriesTotal++;
                    if (fulfilled) kpis.DeliveriesFulfilled++;
                    else kpis.DeliveriesPending++;
                }
                else
                {
                    kpis.PickupsTotal++;
                    if (fulfilled) kpis.PickupsFulfilled++;
                    else kpis.PickupsPending++;
                }
            }

            return kpis;
        }

        private static OrdersReportRowDto BuildRow(Order o)
        {
            var supplyDate = (o.DeliveryDate ?? o.PickupDate ?? o.CreationTime).Date;
            return new OrdersReportRowDto
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber ?? "",
                CustomerName = NullIfWhiteSpace(o.DeliveryRecipientName) ?? NullIfWhiteSpace(o.CustomerName),
                Phone = NullIfWhiteSpace(o.DeliveryRecipientPhone) ?? NullIfWhiteSpace(o.CustomerPhone),
                Street = NullIfWhiteSpace(o.DeliveryStreet) ?? NullIfWhiteSpace(o.DeliveryAddress),
                Floor = NullIfWhiteSpace(o.DeliveryFloor),
                Apartment = NullIfWhiteSpace(o.DeliveryApartment),
                EntranceCode = NullIfWhiteSpace(o.DeliveryEntranceCode),
                City = NullIfWhiteSpace(o.DeliveryCity),
                DeliveryType = IsShipping(o) ? "shipping" : "pickup",
                Status = o.Status ?? "",
                IsFulfilled = IsFulfilled(o),
                SupplyDateLocal = supplyDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                SupplyTime = NullIfWhiteSpace(IsShipping(o) ? o.DeliveryTime : o.PickupTime)
                    ?? NullIfWhiteSpace(o.DeliveryTime) ?? NullIfWhiteSpace(o.PickupTime),
                PaymentKind = ResolvePaymentKind(o),
                PaymentLabel = NullIfWhiteSpace(o.PaymentLabel)
                    ?? NullIfWhiteSpace(o.PaymentMethodTitle) ?? NullIfWhiteSpace(o.PaymentMethod),
                DeliveryNote = NullIfWhiteSpace(o.DeliveryNote),
                CustomerNote = NullIfWhiteSpace(o.CustomerNote),
                Total = o.Total ?? 0m,
                Source = o.Source ?? "",
            };
        }

        private static bool IsShipping(Order o) =>
            string.Equals(o.DeliveryType?.Trim(), "Shipping", StringComparison.OrdinalIgnoreCase);

        /// <summary>Handed over: shipping → left with the courier, pickup → supplied to the customer.</summary>
        private static bool IsFulfilled(Order o)
        {
            var s = o.Status?.Trim();
            return string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, "Delivered", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// In-memory mirror of the orders-list cash/credit heuristic (OrderStorage.ApplyOrderListFilter):
        /// cash markers win; Cardcom / credit markers → credit (ExternalCredit counts as credit - a card charge).
        /// </summary>
        private static string ResolvePaymentKind(Order o)
        {
            var method = (o.PaymentMethod ?? "").Trim().ToLowerInvariant();
            var label = o.PaymentLabel ?? "";
            var gatewayCode = (o.GatewayPaymentMethodCode ?? "").Trim().ToLowerInvariant();

            var cash =
                method == "cash" || method == "cod" || method == "onaccount" || method == "banktransfer" ||
                (o.PaymentMethod?.Contains("מזומן") ?? false) ||
                (string.IsNullOrEmpty(method) && (gatewayCode == "cod" || label.Contains("מזומן")));
            if (cash)
                return "cash";

            var credit =
                method.Contains("credit") || method == "savedcard" ||
                (o.PaymentMethod?.Contains("אשראי") ?? false) ||
                string.Equals(o.PaymentGateway?.Trim(), "cardcom", StringComparison.OrdinalIgnoreCase) ||
                o.CardcomLowProfileId != null;
            return credit ? "credit" : "other";
        }

        /// <summary>
        /// Minutes-of-day of the first HH:mm in the time window ("9:00-11:00" → 540), so times sort
        /// numerically rather than as strings ("9:00" before "10:00" would fail ordinal compare).
        /// Orders without a time sort last within their day.
        /// </summary>
        private static int SupplyTimeSortKey(string? supplyTime)
        {
            var m = System.Text.RegularExpressions.Regex.Match(supplyTime ?? "", @"(\d{1,2}):(\d{2})");
            if (!m.Success)
                return int.MaxValue;
            var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return h * 60 + min;
        }

        private static bool Contains(string? value, string term) =>
            value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static DateTime IsraelToday()
        {
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
            }
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
    }
}
