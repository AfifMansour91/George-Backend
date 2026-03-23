namespace George.Services.Request;

/// <summary>Request to enqueue a print job (e.g. order voucher) for the local print agent.</summary>
public class CreatePrintJobReq
{
    public int SiteId { get; set; }
    public int? OrderId { get; set; }
    public string JobType { get; set; } = "Voucher";
    public string? Trigger { get; set; }
    public string? ClientSource { get; set; }
    public string Payload { get; set; } = "";
}
