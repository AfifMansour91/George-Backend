namespace George.Services.Request;

/// <summary>Base media request (shared fields).</summary>
public class MediaReq
{
    public string Url { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Type { get; set; }
    public int? BusinessTypeId { get; set; }
    public List<int>? CategoryIds { get; set; }
    public List<int>? SubcategoryIds { get; set; }
    public List<string>? Tags { get; set; }
    public long? FileSize { get; set; }
    public int? UsageCount { get; set; }
}

public class CreateMediaReq : MediaReq
{
    /// <summary>When set, media is linked to this account (and SiteId when provided) for the account/site media library.</summary>
    public int? AccountId { get; set; }
    /// <summary>When set with AccountId, media is scoped to this site so other sites under the account do not see it.</summary>
    public int? SiteId { get; set; }
}

public class UpdateMediaReq : CreateMediaReq
{
    public int Id { get; set; }
}

/// <summary>Request to record that an account/site uses a media item (e.g. "Add to my media").</summary>
public class UseMediaReq
{
    public int AccountId { get; set; }
    /// <summary>When set, links media to this site only. When null, first site of the account is used (backward compat).</summary>
    public int? SiteId { get; set; }
}

public class DownloadAndSaveMediaReq
{
    public List<int> MediaIds { get; set; } = new();
}
