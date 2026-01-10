namespace George.Services.Response
{
    /// <summary>
    /// Response model for bulk template product import
    /// </summary>
    public class BulkImportTemplateProductRes
    {
        public int Total { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<BulkImportTemplateProductItemRes> Results { get; set; } = new();
    }

    /// <summary>
    /// Result for a single template product in bulk import
    /// </summary>
    public class BulkImportTemplateProductItemRes
    {
        public string? Sku { get; set; }
        public string Name { get; set; } = null!;
        public bool Success { get; set; }
        public string Action { get; set; } = null!; // "created" | "updated" | "failed"
        public string? ErrorMessage { get; set; }
        public int? ProductId { get; set; }
    }
}
