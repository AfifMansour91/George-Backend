using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    /// <summary>
    /// Request body for create/update of a Brand (account/site-scoped).
    /// Mirrors CategoryReq with brand-specific fields (Slug, SeoTitle, SeoDescription,
    /// WooCommerceBrandId, SourceGlobalBrandId).
    /// </summary>
    public class BrandReq
    {
        [Required]
        public string Name { get; set; } = null!;

        /// <summary>URL-friendly identifier. Auto-generated from Name if not provided.</summary>
        public string? Slug { get; set; }

        public string? Description { get; set; }

        public int? ParentBrandId { get; set; }

        public int? SortOrder { get; set; }

        public bool? IsEnabled { get; set; }

        public int? AccountId { get; set; }

        public List<int>? SiteIds { get; set; }

        /// <summary>Brand logo / thumbnail URL.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Optional small icon URL (kiosk/sidebar style).</summary>
        public string? IconUrl { get; set; }

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        /// <summary>Pre-existing WooCommerce taxonomy term id; usually null on create — sync fills it in.</summary>
        public int? WooCommerceBrandId { get; set; }

        /// <summary>If this brand was copied down from a GlobalBrand, the source id.</summary>
        public int? SourceGlobalBrandId { get; set; }
    }

    public class CreateBrandReq : BrandReq
    {
    }

    public class UpdateBrandReq : BrandReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
