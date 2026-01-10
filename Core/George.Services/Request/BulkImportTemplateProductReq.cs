using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    /// <summary>
    /// Request model for bulk template product import with global category creation support
    /// </summary>
    public class BulkImportTemplateProductReq
    {
        [Required]
        public List<BulkImportTemplateProductItemReq> Products { get; set; } = new();

        /// <summary>
        /// Whether to update existing products if found by SKU
        /// </summary>
        public bool UpdateIfExists { get; set; } = true;

        /// <summary>
        /// Whether to create global categories if they don't exist
        /// </summary>
        public bool CreateCategoriesIfNotExists { get; set; } = true;

        /// <summary>
        /// Site IDs to assign products to
        /// </summary>
        public List<int>? SiteIds { get; set; }
    }

    /// <summary>
    /// Template product item for bulk import with category path support
    /// </summary>
    public class BulkImportTemplateProductItemReq : CreateTemplateProductReq
    {
        /// <summary>
        /// Global category paths (e.g., "בקר > נתחים" or "Electronics") - will be resolved to GlobalCategory IDs
        /// </summary>
        public List<string>? CategoryPaths { get; set; }
    }
}
