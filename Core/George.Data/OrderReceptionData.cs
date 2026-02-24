namespace George.Data;

/// <summary>Order reception state for a site (get). Used by OrderReceptionStorage; service maps to OrderReceptionRes.</summary>
public class OrderReceptionData
{
    public int SiteId { get; set; }
    public bool TodayDeliveryClosed { get; set; }
    public bool TodayPickupClosed { get; set; }
    public List<string> FutureDeliveryDates { get; set; } = new();
    public List<string> FuturePickupDates { get; set; } = new();
}

/// <summary>Order reception update payload. Service maps from OrderReceptionReq.</summary>
public class OrderReceptionUpdateData
{
    public bool? TodayDeliveryClosed { get; set; }
    public bool? TodayPickupClosed { get; set; }
    public List<string>? FutureDeliveryDates { get; set; }
    public List<string>? FuturePickupDates { get; set; }
}
