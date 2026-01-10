using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    /// <summary>
    /// Request model for bulk product import with category creation support
    /// </summary>
    public class BulkImportProductReq
    {
        [Required]
        public List<BulkImportProductItemReq> Products { get; set; } = new();

        /// <summary>
        /// Whether to update existing products if found by SKU
        /// </summary>
        public bool UpdateIfExists { get; set; } = true;

        /// <summary>
        /// Whether to create categories if they don't exist
        /// </summary>
        public bool CreateCategoriesIfNotExists { get; set; } = true;

        /// <summary>
        /// Site IDs to assign categories/products to
        /// </summary>
        public List<int>? SiteIds { get; set; }
    }

    /// <summary>
    /// Product item for bulk import with category path support
    /// </summary>
    public class BulkImportProductItemReq : CreateProductReq
    {
        /// <summary>
        /// Category paths (e.g., "בקר > נתחים" or "Electronics") - will be resolved to IDs
        /// </summary>
        public List<string>? CategoryPaths { get; set; }
    }

    /// <summary>
    /// Category to create with optional parent
    /// </summary>
    public class CategoryToCreateReq
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? ParentCategoryPath { get; set; }

        public string? Description { get; set; }

        public List<int>? SiteIds { get; set; }
    }
}
