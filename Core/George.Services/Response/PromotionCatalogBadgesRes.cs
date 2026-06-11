namespace George.Services.Response;

/// <summary>Which catalog items should show a "במבצע" badge for a site + channel.</summary>
public class PromotionCatalogBadgesRes
{
    public List<int> ProductIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    /// <summary>True when at least one badge promotion applies to all products / whole cart.</summary>
    public bool AllProducts { get; set; }
}
