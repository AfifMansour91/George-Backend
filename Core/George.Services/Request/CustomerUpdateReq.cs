namespace George.Services.Request;

/// <summary>CRM: Update customer (name, permanent note, contact, address, marketing). Null fields are left unchanged.</summary>
public class CustomerUpdateReq
{
    public string Name { get; set; } = "";
    /// <summary>Permanent manager note for this customer (CRM Customer.Notes).</summary>
    public string? Notes { get; set; }
    public string? Email { get; set; }
    /// <summary>When changed, NormalizedPhone is recomputed; rejected if it collides with another customer at the site.</summary>
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    public bool? MarketingEmail { get; set; }
    public bool? MarketingSms { get; set; }
    /// <summary>"חשבונית על שם אחר": business name for invoices. Null = unchanged; "" = clear (invoices use the customer name).</summary>
    public string? InvoiceName { get; set; }
    /// <summary>ח.פ / ע.מ printed with <see cref="InvoiceName"/>. Null = unchanged; "" = clear.</summary>
    public string? InvoiceTaxId { get; set; }
}
