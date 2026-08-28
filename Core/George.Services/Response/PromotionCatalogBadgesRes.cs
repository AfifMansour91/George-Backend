namespace George.Services.Response;

/// <summary>Which catalog items should show a "במבצע" badge for a site + channel.</summary>
public class PromotionCatalogBadgesRes
{
    /// <summary>Per-promotion badge scopes - prefer this over legacy merged fields.</summary>
    public List<PromotionCatalogBadgeRuleRes> Rules { get; set; } = new();

    /// <summary>Legacy merged scope - prefer <see cref="Rules"/>.</summary>
    public List<int> ProductIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public bool AllProducts { get; set; }
}
