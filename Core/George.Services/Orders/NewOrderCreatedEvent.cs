namespace George.Services.Orders;

/// <summary>SignalR payload for <c>NewOrderCreated</c> (camelCase on wire).</summary>
public class NewOrderCreatedEvent
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int AccountId { get; set; }
    public int SiteId { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "New";
    public DateTime CreationTime { get; set; }
}
