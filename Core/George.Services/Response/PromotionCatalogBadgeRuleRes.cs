namespace George.Services.Response;

/// <summary>One ShowBadge promotion's catalog scope (evaluated per rule so exclusions apply correctly).</summary>
public class PromotionCatalogBadgeRuleRes
{
    public int PromotionId { get; set; }
    /// <summary>Catalog banner text - promotion name (matches WP plugin <c>promeng-banner</c>).</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>discount | buy_x_pay_y | buy_x_get_y</summary>
    public string? PromotionType { get; set; }
    /// <summary>For discount: percent | amount.</summary>
    public string? DiscountKind { get; set; }
    public decimal? DiscountValue { get; set; }
    public bool WholeCart { get; set; }
    public bool AllProducts { get; set; }
    public List<int> ProductIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public List<int> ExcludedProductIds { get; set; } = new();
}
