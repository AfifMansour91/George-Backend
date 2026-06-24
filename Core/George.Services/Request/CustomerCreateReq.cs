namespace George.Services.Request;

/// <summary>CRM: Create a new customer manually from the customers screen.</summary>
public class CustomerCreateReq
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
}
