namespace George.Services.Response;

/// <summary>CRM: Customer detail (extends CustomerRes with address, marketing, tags, activity).</summary>
public class CustomerDetailRes : CustomerRes
{
    public string? DefaultAddress { get; set; }
    /// <summary>Structured delivery fields (same semantics as order shipping).</summary>
    public string? DeliveryStreet { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    public List<string>? AddressLines { get; set; }
    public bool? MarketingEmail { get; set; }
    public bool? MarketingSms { get; set; }
    public string? MarketingEmailRegisteredAt { get; set; }
    public List<string>? Tags { get; set; }
    public string? LastEditedNoteAt { get; set; }
    public string? LastOrderDate { get; set; }
    /// <summary>Average days between consecutive (non-cancelled) orders for this customer.</summary>
    public int AverageReturnDays { get; set; }
    public List<CustomerActivityItem>? Activity { get; set; }
}

/// <summary>CRM: Activity timeline item.</summary>
public class CustomerActivityItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Date { get; set; } = string.Empty;
    public string? Type { get; set; }
}
