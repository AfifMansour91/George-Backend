using George.DB;
using Xunit;

namespace George.Services.Tests;

public class CustomerLabelHtmlTests
{
    private static Order ShippingOrder() => new()
    {
        Id = 9001,
        SiteId = 45,
        OrderNumber = "12345",
        CustomerName = "דנה לוי",
        CustomerPhone = "0501234567",
        DeliveryType = "Shipping",
        DeliveryStreet = "הרצל 12",
        DeliveryCity = "תל אביב",
        DeliveryFloor = "3",
        DeliveryApartment = "7",
        DeliveryDate = new DateTime(2026, 9, 10), // Thursday
        PaymentMethod = "cash",
    };

    [Fact]
    public void Standard_ShowsOrderNumberNamePhoneAndShippingAddress()
    {
        var html = CustomerLabelHtml.BuildStandard(ShippingOrder());

        Assert.Contains("#12345", html);
        Assert.Contains("דנה לוי", html);
        Assert.Contains("0501234567", html);
        Assert.Contains("הרצל 12, תל אביב, 7, 3", html);
        Assert.Contains("size: 58mm 40mm", html);
        Assert.DoesNotContain("מזמין:", html);
    }

    [Fact]
    public void Standard_PickupOrder_OmitsAddress()
    {
        var order = ShippingOrder();
        order.DeliveryType = "Pickup";

        var html = CustomerLabelHtml.BuildStandard(order);

        Assert.DoesNotContain("class=\"address\"", html);
    }

    [Fact]
    public void Wide_ShipToOtherPerson_LeadsWithRecipientAndKeepsOrdererRow()
    {
        var order = ShippingOrder();
        order.DeliveryRecipientName = "יוסי כהן";
        order.DeliveryRecipientPhone = "0529876543";
        order.PaymentMethod = "onaccount";

        var html = CustomerLabelHtml.BuildWide(order);

        Assert.Contains("size: 120mm 80mm", html);
        Assert.Contains("<div class=\"customer-line\">יוסי כהן", html);
        Assert.Contains("0529876543", html);
        Assert.Contains("מזמין:</span> <span class=\"row-value\">דנה לוי 0501234567", html);
        Assert.Contains("משלוח", html);
        Assert.Contains("קומה: 3", html);
        Assert.Contains("דירה: 7", html);
        Assert.Contains("יום אספקה:</span> <span class=\"row-value\"><span class=\"seg\">חמישי 10/09", html);
        Assert.Contains("שיטת תשלום:</span> <span class=\"row-value\">בהקפה", html);
        Assert.DoesNotContain("שעת איסוף", html);
    }

    [Fact]
    public void Wide_PickupOrder_ShowsPickupTimeAndCreditLabel()
    {
        var order = ShippingOrder();
        order.DeliveryType = "Pickup";
        order.PickupDate = new DateTime(2026, 9, 11);
        order.PickupTime = "14:00-16:00";
        order.PaymentMethod = "CreditSms";

        var html = CustomerLabelHtml.BuildWide(order);

        Assert.Contains("איסוף עצמי", html);
        Assert.Contains("שעת איסוף:</span> <span class=\"row-value\"><span class=\"seg\">14:00-16:00", html);
        Assert.Contains("שישי 11/09", html);
        Assert.Contains("שיטת תשלום:</span> <span class=\"row-value\">אשראי", html);
        Assert.DoesNotContain("כתובת:", html);
    }

    [Fact]
    public void EscapesHtmlInCustomerFields()
    {
        var order = ShippingOrder();
        order.CustomerName = "<b>x</b>";

        var html = CustomerLabelHtml.BuildStandard(order);

        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
    }
}
