namespace George.Services.Response;

/// <summary>CRM: Result of a customer bulk-import batch.</summary>
public class CustomerImportRes
{
    public int Total { get; set; }
    public int Created { get; set; }
    /// <summary>Existing customers enriched (matched by phone or email).</summary>
    public int Updated { get; set; }
    /// <summary>Rows skipped: no phone AND no email, duplicate of an earlier row in the same request, or matched a soft-deleted customer.</summary>
    public int Skipped { get; set; }
    public int Failed { get; set; }
    /// <summary>Per-row messages for skipped/failed rows only (keyed by the row's name/phone for display).</summary>
    public List<CustomerImportRowIssueRes> Issues { get; set; } = new();
}

public class CustomerImportRowIssueRes
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>"skipped" | "failed"</summary>
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
}
