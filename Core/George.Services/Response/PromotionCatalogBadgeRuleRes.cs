namespace George.Services.Response;

/// <summary>One ShowBadge promotion's catalog scope (evaluated per rule so exclusions apply correctly).</summary>
public class PromotionCatalogBadgeRuleRes
{
    public bool AllProducts { get; set; }
    public List<int> ProductIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public List<int> ExcludedProductIds { get; set; } = new();
}
