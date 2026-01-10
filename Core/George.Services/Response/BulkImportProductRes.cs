namespace George.Services.Response
{
    /// <summary>
    /// Response model for bulk product import
    /// </summary>
    public class BulkImportProductRes
    {
        public int Total { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public List<BulkImportProductItemRes> Results { get; set; } = new();
    }

    /// <summary>
    /// Result for a single product in bulk import
    /// </summary>
    public class BulkImportProductItemRes
    {
        public string? Sku { get; set; }
        public string Name { get; set; } = null!;
        public bool Success { get; set; }
        public string Action { get; set; } = null!; // "created" | "updated" | "failed"
        public string? ErrorMessage { get; set; }
        public int? ProductId { get; set; }
    }
}
