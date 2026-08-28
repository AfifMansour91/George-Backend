namespace George.Services.Request;

/// <summary>
/// Inbound batch from WordPress Promeng plugin - <c>POST /redemptions</c> (GIORGIO_API §2.1).
/// </summary>
public class RecordPromotionRedemptionsReq
{
    public List<PromotionRedemptionRowReq>? Redemptions { get; set; }
}

public class PromotionRedemptionRowReq
{
    public string? PromotionExternalId { get; set; }
    public string? PromotionName { get; set; }
    public long? OrderId { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? Channel { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Local or UTC timestamp string from the plugin (<c>YYYY-MM-DD HH:MM:SS</c>).</summary>
    public string? RedeemedAt { get; set; }
}
