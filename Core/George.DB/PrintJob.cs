namespace George.DB;

/// <summary>
/// Print job for local print agent (multi-branch silent printing).
/// React enqueues via API; local agent at branch polls and prints to default printer or IP:9100.
/// </summary>
public class PrintJob
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public int? OrderId { get; set; }
    public string JobType { get; set; } = "Voucher";
    public string Payload { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? AgentId { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual Site? Site { get; set; }
}
