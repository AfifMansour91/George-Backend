namespace George.Services.Response;

/// <summary>Sprint 2: Order reception open/close state for a site (פתיחה/סגירת קבלת הזמנות).</summary>
public class OrderReceptionRes
{
    public int SiteId { get; set; }
    /// <summary>True if delivery is closed for today.</summary>
    public bool TodayDeliveryClosed { get; set; }
    /// <summary>True if pickup is closed for today.</summary>
    public bool TodayPickupClosed { get; set; }
    /// <summary>Future dates (date only, ISO YYYY-MM-DD) when delivery is closed. Excludes past dates.</summary>
    public List<string> FutureDeliveryDates { get; set; } = new();
    /// <summary>Future dates when pickup is closed. Excludes past dates.</summary>
    public List<string> FuturePickupDates { get; set; } = new();
}
