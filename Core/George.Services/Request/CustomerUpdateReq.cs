namespace George.Services.Request;

/// <summary>CRM: Update customer (e.g. name).</summary>
public class CustomerUpdateReq
{
    public string Name { get; set; } = "";
    /// <summary>Permanent manager note for this customer (CRM Customer.Notes).</summary>
    public string? Notes { get; set; }
}
