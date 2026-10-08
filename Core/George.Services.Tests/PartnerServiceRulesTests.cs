using George.Data;
using George.DB;
using George.Services.Partner;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Partner API (/Partner/v1) pure rules: delivery-fee resolution, supply-date validation and the order
/// mapping the vendor sees (API responses and webhooks share it). docs/PARTNER_API.md.
/// </summary>
public class PartnerServiceRulesTests
{
    private static readonly List<SiteCityShippingCost> Cities = new()
    {
        new SiteCityShippingCost { City = "תל אביב", Cost = 25m },
        new SiteCityShippingCost { City = "רחובות ", Cost = 15m },
    };

    [Fact]
    public void ShippingFee_FreeAboveThreshold_UsesSubtotalBeforeDiscounts()
    {
        var site = new Site { ShippingCost = 30m, FreeShippingAbove = 300m };
        var (fee, free) = PartnerService.ResolveShippingFee(site, Cities, "תל אביב", 300m);
        Assert.Equal(0m, fee);
        Assert.True(free);
    }

    [Fact]
    public void ShippingFee_CityMatch_IsTrimmedAndCaseInsensitive()
    {
        var site = new Site { ShippingCost = 30m, FreeShippingAbove = 300m };
        var (fee, free) = PartnerService.ResolveShippingFee(site, Cities, " רחובות", 100m);
        Assert.Equal(15m, fee);
        Assert.False(free);
    }

    [Fact]
    public void ShippingFee_UnknownCity_FallsBackToSiteDefault()
    {
        var site = new Site { ShippingCost = 30m };
        var (fee, _) = PartnerService.ResolveShippingFee(site, Cities, "חיפה", 100m);
        Assert.Equal(30m, fee);
        Assert.Equal(0m, PartnerService.ResolveShippingFee(new Site(), Cities, null, 100m).Fee);
    }

    [Fact]
    public void SupplyDate_PastToday_ClosedDay_AndOpenDay()
    {
        var today = new DateOnly(2026, 9, 28);
        var reception = new OrderReceptionData
        {
            TodayPickupClosed = true,
            FutureDeliveryDates = new List<string> { "2026-10-02" },
        };

        Assert.Equal(PartnerErrorCode.SupplyDateInPast, PartnerService.SupplyDateProblem(reception, isPickup: true, today.AddDays(-1), today)!.Value.Code);
        Assert.Equal(PartnerErrorCode.ShopClosed, PartnerService.SupplyDateProblem(reception, isPickup: true, today, today)!.Value.Code);
        // Delivery today is open (only pickup is closed today).
        Assert.Null(PartnerService.SupplyDateProblem(reception, isPickup: false, today, today));
        Assert.Equal(PartnerErrorCode.ShopClosed, PartnerService.SupplyDateProblem(reception, isPickup: false, new DateOnly(2026, 10, 2), today)!.Value.Code);
        Assert.Null(PartnerService.SupplyDateProblem(reception, isPickup: true, new DateOnly(2026, 10, 2), today));
    }

    [Fact]
    public void OrderMapper_DiscountTotal_Timestamps_CanCancel_AndBundleChildrenHidden()
    {
        var created = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
        var parent = new OrderItem { Id = 1, ProductId = 10, Title = "מארז", Quantity = 1, TotalPrice = 200m, BundleProductId = 10, SortOrder = 0 };
        var child = new OrderItem { Id = 2, ProductId = 11, Title = "רכיב", Quantity = 1, TotalPrice = 0m, ParentOrderItemId = 1, SortOrder = 1 };
        var plain = new OrderItem { Id = 3, ProductId = 12, Title = "סלמון", Quantity = 1.5m, PricePerUnit = 100m, TotalPrice = 150m, DiscountAmount = 15m, SortOrder = 2, LineSku = "SAL-1" };
        var order = new Order
        {
            Id = 77,
            OrderNumber = "1077",
            Source = "WhatsApp",
            Status = "InTreatment",
            PaymentStatus = "Unpaid",
            ExternalOrderId = "wa:abc:1",
            SubTotal = 350m,
            ShippingCost = 25m,
            Total = 360m,
            ManualDiscountAmount = 0m,
            CreationTime = created,
            DeliveryProviderTrackingLink = "https://track/1",
            OrderItem = new List<OrderItem> { parent, child, plain },
        };
        var history = new List<(string Status, DateTime OccurredAt)>
        {
            ("New", created),
            ("InTreatment", created.AddMinutes(10)),
        };

        var res = PartnerOrderMapper.Map(order, history, alreadyExisted: true);

        Assert.Equal(77, res.OrderId);
        Assert.Equal("wa:abc:1", res.PartnerRef);
        Assert.True(res.AlreadyExisted);
        Assert.Equal(15m, res.DiscountTotal);
        Assert.False(res.CanCancel);
        Assert.Equal(created.AddMinutes(10), res.InTreatmentAt);
        Assert.Null(res.ReadyAt);
        Assert.Equal("https://track/1", res.DeliveryTrackingLink);
        Assert.Equal(2, res.Items.Count);
        Assert.DoesNotContain(res.Items, i => i.OrderItemId == 2);
        Assert.Equal("SAL-1", res.Items.Single(i => i.OrderItemId == 3).Sku);
    }

    [Fact]
    public void OrderMapper_NewOrderCanCancel_CancelledCarriesTimestamp()
    {
        var order = new Order { Id = 1, OrderNumber = "1", Source = "Partner", Status = "New", PaymentStatus = "Unpaid", CreationTime = DateTime.UtcNow };
        Assert.True(PartnerOrderMapper.Map(order, null, false).CanCancel);

        order.Status = "Cancelled";
        order.UpdatedDate = order.CreationTime.AddHours(1);
        var cancelled = PartnerOrderMapper.Map(order, null, false);
        Assert.False(cancelled.CanCancel);
        Assert.Equal(order.UpdatedDate, cancelled.CancelledAt);
    }

    [Theory]
    [InlineData("WhatsApp", true)]
    [InlineData("partner", true)]
    [InlineData("Website", false)]
    [InlineData("Phone", false)]
    [InlineData(null, false)]
    public void PartnerSource_OnlyWhatsAppAndPartner(string? source, bool expected)
    {
        Assert.Equal(expected, PartnerOrderMapper.IsPartnerSource(source));
    }
}
