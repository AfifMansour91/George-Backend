using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    /// <summary>
    /// Request body for create/update of a GlobalBrand (super-admin / platform-wide).
    /// Mirrors GlobalCategoryReq with brand-specific fields.
    /// </summary>
    public class GlobalBrandReq
    {
        [Required]
        public string Name { get; set; } = null!;

        /// <summary>URL-friendly identifier. Auto-generated from Name if blank.</summary>
        public string? Slug { get; set; }

        public string? Description { get; set; }

        public int? ParentGlobalBrandId { get; set; }

        public int? SortOrder { get; set; }

        /// <summary>Aggregate display only; not authoritative — read from product joins when needed.</summary>
        public int? ProductCount { get; set; }

        public string? ImageUrl { get; set; }

        public string? IconUrl { get; set; }

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        /// <summary>Pre-existing WooCommerce taxonomy term id (rarely set on create).</summary>
        public int? WooCommerceBrandId { get; set; }
    }

    public class CreateGlobalBrandReq : GlobalBrandReq
    {
    }

    public class UpdateGlobalBrandReq : GlobalBrandReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
