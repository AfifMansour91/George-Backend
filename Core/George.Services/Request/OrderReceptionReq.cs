namespace George.Services.Request;

/// <summary>Sprint 2: Update order reception (today toggles + future dates).</summary>
public class OrderReceptionReq
{
    /// <summary>True to close delivery for today.</summary>
    public bool? TodayDeliveryClosed { get; set; }
    /// <summary>True to close pickup for today.</summary>
    public bool? TodayPickupClosed { get; set; }
    /// <summary>Future dates (YYYY-MM-DD) for delivery. Replaces existing future delivery dates. Past dates are ignored.</summary>
    public List<string>? FutureDeliveryDates { get; set; }
    /// <summary>Future dates for pickup. Replaces existing.</summary>
    public List<string>? FuturePickupDates { get; set; }
}
