using George.Services.Delivery;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

public class LionWheelDeliveryProviderTests
{
    private static LionWheelDeliveryProvider CreateProvider() =>
        new(null!, NullLogger<LionWheelDeliveryProvider>.Instance);

    [Theory]
    [InlineData("הרצל 12", "הרצל", "12")]
    [InlineData("הרצל 12א", "הרצל", "12א")]
    [InlineData("שדרות רוטשילד, 45", "שדרות רוטשילד", "45")]
    [InlineData("רחוב בלי מספר", "רחוב בלי מספר", "")]
    [InlineData("", "", "")]
    public void SplitStreetAndNumber_PeelsTrailingHouseNumber(string input, string expectedStreet, string expectedNumber)
    {
        var (street, number) = LionWheelDeliveryProvider.SplitStreetAndNumber(input);
        Assert.Equal(expectedStreet, street);
        Assert.Equal(expectedNumber, number);
    }

    [Fact]
    public void ParseWebhookStatus_NumericStatus_MapsToName()
    {
        var parsed = CreateProvider().ParseWebhookStatus("{\"task_id\": 123, \"status\": 2}");
        Assert.NotNull(parsed);
        Assert.Equal("123", parsed!.Value.TaskId);
        Assert.Equal("active", parsed.Value.CourierStatus);
    }

    [Fact]
    public void ParseWebhookStatus_NumericStringStatus_MapsToName()
    {
        var parsed = CreateProvider().ParseWebhookStatus("{\"task_id\": \"456\", \"status\": \"3\"}");
        Assert.NotNull(parsed);
        Assert.Equal("456", parsed!.Value.TaskId);
        Assert.Equal("completed", parsed.Value.CourierStatus);
    }

    [Fact]
    public void ParseWebhookStatus_WrappedTaskObject_IsUnwrapped()
    {
        var parsed = CreateProvider().ParseWebhookStatus("{\"task\": {\"task_id\": 9, \"status\": 4}}");
        Assert.NotNull(parsed);
        Assert.Equal("9", parsed!.Value.TaskId);
        Assert.Equal("cancelled", parsed.Value.CourierStatus);
    }

    /// <summary>Live tasks/show shape (verified 2026-09-08): "id" field, uppercase name, CANCELED single-L.</summary>
    [Fact]
    public void ParseWebhookStatus_LiveApiShape_IdFieldAndUppercaseName()
    {
        var parsed = CreateProvider().ParseWebhookStatus("{\"task\": {\"id\": 27767477, \"status\": \"CANCELED\"}}");
        Assert.NotNull(parsed);
        Assert.Equal("27767477", parsed!.Value.TaskId);
        Assert.Equal("cancelled", parsed.Value.CourierStatus);
    }

    [Fact]
    public void ParseWebhookStatus_UppercaseStatusName_IsLowercased()
    {
        var parsed = CreateProvider().ParseWebhookStatus("{\"task\": {\"id\": 5, \"status\": \"UNASSIGNED\"}}");
        Assert.NotNull(parsed);
        Assert.Equal("unassigned", parsed!.Value.CourierStatus);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"task_id\": 1}")]
    public void ParseWebhookStatus_UnusablePayload_ReturnsNull(string payload)
    {
        Assert.Null(CreateProvider().ParseWebhookStatus(payload));
    }

    [Fact]
    public void BuildCreateTaskPayload_UsesDdMmYyyyDate_AndCompanyIdFromSettings()
    {
        var order = new George.DB.Order
        {
            Id = 1993,
            OrderNumber = "1993",
            DeliveryDate = new DateTime(2026, 9, 8),
            DeliveryStreet = "הרצל 12",
            DeliveryCity = "תל אביב",
            CustomerName = "לקוח בדיקה",
            CustomerPhone = "0500000000",
        };
        var config = new George.DB.DeliveryProviderConfig
        {
            SiteId = 25,
            ProviderKey = "lionwheel",
            SettingsJson = "{\"company_id\":\"123\"}",
        };

        var payload = LionWheelDeliveryProvider.BuildCreateTaskPayload(order, config);

        Assert.Equal("08/09/2026", payload["pickup_at"]);
        Assert.Equal("123", payload["company_id"]);
        Assert.Equal("הרצל", payload["destination_street"]);
        Assert.Equal("12", payload["destination_number"]);
    }

    [Fact]
    public void BuildCreateTaskPayload_NoSettings_OmitsCompanyId()
    {
        var order = new George.DB.Order { Id = 1, DeliveryDate = new DateTime(2026, 1, 2) };
        var config = new George.DB.DeliveryProviderConfig { SiteId = 1, ProviderKey = "lionwheel" };

        var payload = LionWheelDeliveryProvider.BuildCreateTaskPayload(order, config);

        Assert.False(payload.ContainsKey("company_id"));
        Assert.Equal("02/01/2026", payload["pickup_at"]);
    }
}
