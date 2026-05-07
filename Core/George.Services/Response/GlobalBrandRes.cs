namespace George.Services.Response
{
    /// <summary>
    /// Response payload for a GlobalBrand. Field names align with the existing
    /// frontend src/api/globalBrandApi.ts so the wire shape matches what the UI expects.
    /// </summary>
    public class GlobalBrandRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }

        public string Name { get; set; } = null!;
        public string? Slug { get; set; }
        public string? Description { get; set; }

        public int? ParentGlobalBrandId { get; set; }
        public int? SortOrder { get; set; }
        public int? ProductCount { get; set; }

        public string? ImageUrl { get; set; }
        public string? IconUrl { get; set; }

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }

        public int? WooCommerceBrandId { get; set; }
    }
}
