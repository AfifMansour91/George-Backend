namespace George.Services.Request;

/// <summary>CRM: Bulk import customers from a spreadsheet (customers screen import button). Frontend parses the file and sends rows in batches.</summary>
public class CustomerImportReq
{
    public List<CustomerImportRowReq> Rows { get; set; } = new();
}

/// <summary>One spreadsheet row. Phone is canonicalized server-side (leading-0 restore, +972 → 0); rows are matched to existing customers by phone, or by email when there is no phone. Existing customers are enriched only (empty fields filled, marketing consent OR-ed) - never overwritten.</summary>
public class CustomerImportRowReq
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    public string? Notes { get; set; }
    /// <summary>Marketing consent from the source file - when true sets MarketingApproval + MarketingEmail + MarketingSms.</summary>
    public bool MarketingApproval { get; set; }
}
