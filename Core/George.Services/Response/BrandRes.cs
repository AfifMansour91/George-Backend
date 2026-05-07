namespace George.Services.Response
{
    /// <summary>
    /// Response payload for a Brand. Mirrors CategoryRes plus brand-specific fields
    /// (Slug, SEO, WooCommerce id).
    /// </summary>
    public class BrandRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }

        public string Name { get; set; } = null!;
        public string? Slug { get; set; }
        public string? Description { get; set; }

        public int? ParentBrandId { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsEnabled { get; set; }

        public int? AccountId { get; set; }
        public List<int> SiteIds { get; set; } = new();

        public string? ImageUrl { get; set; }
        public string? IconUrl { get; set; }

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }

        public int? WooCommerceBrandId { get; set; }
        public int? SourceGlobalBrandId { get; set; }

        /// <summary>
        /// Optional aggregate: number of products linked to this brand. Filled in by the storage
        /// layer when requested; otherwise null.
        /// </summary>
        public int? ProductCount { get; set; }
    }
}
