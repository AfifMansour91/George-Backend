namespace George.Services.Response
{
    public class OrdersReportRangeDto
    {
        /// <summary>Local calendar start <c>yyyy-MM-dd</c> (same semantics as SPA query).</summary>
        public string FromLocal { get; set; } = "";

        /// <summary>Inclusive end date <c>yyyy-MM-dd</c>.</summary>
        public string ToLocal { get; set; } = "";
    }

    public class OrdersReportKpisDto
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalShippingCost { get; set; }
        public int DeliveriesTotal { get; set; }
        /// <summary>Shipping orders already handed over / delivered (Completed or Delivered).</summary>
        public int DeliveriesFulfilled { get; set; }
        public int DeliveriesPending { get; set; }
        public int PickupsTotal { get; set; }
        /// <summary>Pickup orders already supplied to the customer (Completed or Delivered).</summary>
        public int PickupsFulfilled { get; set; }
        public int PickupsPending { get; set; }
    }

    public class OrdersReportRowDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = "";
        /// <summary>Recipient name when the order ships to another person, else the customer name.</summary>
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        /// <summary>Street + house number (DeliveryStreet, falling back to the free-text DeliveryAddress).</summary>
        public string? Street { get; set; }
        public string? Floor { get; set; }
        public string? Apartment { get; set; }
        public string? EntranceCode { get; set; }
        public string? City { get; set; }
        /// <summary><c>shipping</c> | <c>pickup</c>.</summary>
        public string DeliveryType { get; set; } = "pickup";
        public string Status { get; set; } = "";
        /// <summary>Order already handed over: shipping → sent, pickup → supplied (Completed/Delivered).</summary>
        public bool IsFulfilled { get; set; }
        /// <summary>Effective supply date <c>yyyy-MM-dd</c> (DeliveryDate, else PickupDate, else CreationTime).</summary>
        public string SupplyDateLocal { get; set; } = "";
        /// <summary>Delivery/pickup time window as stored (e.g. "10:00-12:00").</summary>
        public string? SupplyTime { get; set; }
        /// <summary><c>cash</c> | <c>credit</c> | <c>other</c> - same semantics as the orders-list payment filter.</summary>
        public string PaymentKind { get; set; } = "other";
        /// <summary>Raw gateway/method title for display next to the kind.</summary>
        public string? PaymentLabel { get; set; }
        public string? DeliveryNote { get; set; }
        public string? CustomerNote { get; set; }
        public decimal Total { get; set; }
        public string Source { get; set; } = "";
    }

    /// <summary>One bundle (מארז) product's sales in the report's orders, from the bundle parent lines (BUNDLES_SYNC_SPEC.md §8).</summary>
    public class OrdersReportBundleRowDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public int OrdersCount { get; set; }
        public decimal UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class OrdersReportBundleRevenueDto
    {
        /// <summary>Distinct orders containing at least one bundle.</summary>
        public int OrdersCount { get; set; }
        public decimal UnitsSold { get; set; }
        public decimal Revenue { get; set; }
        /// <summary>Bundle revenue / <see cref="OrdersReportKpisDto.TotalRevenue"/> (0..1).</summary>
        public decimal ShareOfRevenue { get; set; }
        /// <summary>Sorted by revenue desc.</summary>
        public List<OrdersReportBundleRowDto> Rows { get; set; } = new();
    }

    public class OrdersReportRes
    {
        public OrdersReportRangeDto Range { get; set; } = new();

        /// <summary>Computed over the filtered window WITHOUT the fulfillment filter, so both sides of the sent/pending split always show.</summary>
        public OrdersReportKpisDto Kpis { get; set; } = new();

        /// <summary>Distinct delivery cities in the date window (before city filter) for the city multi-select.</summary>
        public List<string> Cities { get; set; } = new();

        /// <summary>True when the window contains orders without a delivery city (city filter sentinel <c>__none__</c>).</summary>
        public bool HasCityNone { get; set; }

        public List<OrdersReportRowDto> Rows { get; set; } = new();

        /// <summary>
        /// Bundles (מארזים) revenue over the same orders as <see cref="Kpis"/> - null unless
        /// <c>Account.BundlesEnabled</c> (UI hides the section).
        /// </summary>
        public OrdersReportBundleRevenueDto? BundleRevenue { get; set; }
    }
}
