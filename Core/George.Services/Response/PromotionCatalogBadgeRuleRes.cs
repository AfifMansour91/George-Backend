namespace George.Services.Response;

/// <summary>One ShowBadge promotion's catalog scope (evaluated per rule so exclusions apply correctly).</summary>
public class PromotionCatalogBadgeRuleRes
{
    public int PromotionId { get; set; }
    /// <summary>Catalog banner text — promotion name (matches WP plugin <c>promeng-banner</c>).</summary>
    public string Label { get; set; } = string.Empty;
    public bool AllProducts { get; set; }
    public List<int> ProductIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public List<int> ExcludedProductIds { get; set; } = new();
}
