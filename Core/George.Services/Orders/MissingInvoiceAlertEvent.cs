namespace George.Services.Orders;

/// <summary>SignalR payload for <c>MissingInvoice</c> (camelCase on wire): a charged card order still has no invoice document.</summary>
public class MissingInvoiceAlertEvent
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int AccountId { get; set; }
    public int SiteId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Hebrew, from the last failed CreateDocument journal row when there is one.</summary>
    public string Reason { get; set; } = "";
    public DateTime? PaidAt { get; set; }
}
