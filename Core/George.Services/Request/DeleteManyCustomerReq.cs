namespace George.Services.Request;

/// <summary>CRM: Delete many customers by id.</summary>
public class DeleteManyCustomerReq
{
    public List<int> Ids { get; set; } = new();
}
