namespace George.Services.Response;

/// <summary>Result of POST /Order/{id}/WoltDispatch (OC Wolt Drive plugin).</summary>
public class WoltDispatchRes
{
    public bool Success { get; set; }
    /// <summary>True when a new delivery was created on this call; false when returning existing state.</summary>
    public bool Created { get; set; }
    public string? WoltTrackingUrl { get; set; }
    public string? WoltTrackingId { get; set; }
    public string? WoltStatus { get; set; }
    public string? WoltDeliveryId { get; set; }
    public DateTime? WoltDispatchedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public OrderRes? Order { get; set; }
}
