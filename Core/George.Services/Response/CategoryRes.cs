namespace George.Services.Response
{
    public class CategoryRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Name { get; set; } = null!;
        public int? ParentCategoryId { get; set; }
        public string? Description { get; set; }
        public string? CustomName { get; set; }
        public bool? IsEnabled { get; set; }
        public int? SortOrder { get; set; }
        public bool? DisplayAsMain { get; set; }
        public int? AccountId { get; set; }
        public List<int> SiteIds { get; set; } = new();
        /// <summary>Kiosk/sidebar: full image URL (Figma Ellipse 4, 260×260 inside circle).</summary>
        public string? ImageUrl { get; set; }
        /// <summary>Kiosk/sidebar: icon URL (Figma fi_XXX, ~134×134 centered in circle).</summary>
        public string? IconUrl { get; set; }
    }
}
