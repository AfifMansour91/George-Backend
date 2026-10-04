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
    /// <summary>How/when the SMS consent was recorded: checkout | manual | import | legacy, ISO-8601 UTC.</summary>
    public string? ConsentSource { get; set; }
    public string? ConsentAt { get; set; }
    /// <summary>Set when the customer opted out of marketing SMS (link | reply | manual); cleared only by an explicit manual re-enable.</summary>
    public string? OptedOutAt { get; set; }
    public string? OptedOutSource { get; set; }
    /// <summary>The marketing send whose unsubscribe link was used, when known.</summary>
    public string? OptedOutSendName { get; set; }
    public string? MarketingEmailRegisteredAt { get; set; }
    public List<string>? Tags { get; set; }
    public string? LastEditedNoteAt { get; set; }
    public string? LastOrderDate { get; set; }
    /// <summary>Average days between consecutive (non-cancelled) orders for this customer.</summary>
    public int AverageReturnDays { get; set; }
    public List<CustomerActivityItem>? Activity { get; set; }
    /// <summary>Phone order: default manual discount type (<c>percent</c> | <c>amount</c>).</summary>
    public string? PermanentDiscountType { get; set; }
    public decimal? PermanentDiscountValue { get; set; }
    /// <summary>"חשבונית על שם אחר": business name printed on invoices instead of the customer name (null = off).</summary>
    public string? InvoiceName { get; set; }
    public string? InvoiceTaxId { get; set; }
    /// <summary>yyyy-MM-dd, for the "birthday this month" segment.</summary>
    public string? BirthDate { get; set; }
}

/// <summary>CRM: Activity timeline item.</summary>
public class CustomerActivityItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    /// <summary>ISO 8601 (UTC) timestamp of the event; the client formats it.</summary>
    public string Date { get; set; } = string.Empty;
    public string? Type { get; set; }
    /// <summary>Display name of the user who performed/added the event (when applicable).</summary>
    public string? ActorName { get; set; }
}
