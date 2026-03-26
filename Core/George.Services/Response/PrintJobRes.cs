namespace George.Services.Response;

public class PrintJobRes
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public int? OrderId { get; set; }
    public string JobType { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string ClientSource { get; set; } = "";

    public string Payload { get; set; } = "";          // base64 or html
    public string PayloadType { get; set; } = "html";  // html | pdf-base64 | png-base64

    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? AgentId { get; set; }
    public string? ErrorMessage { get; set; }
}
