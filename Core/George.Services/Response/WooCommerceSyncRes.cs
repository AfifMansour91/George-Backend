namespace George.Services.Response
{
    public class WooCommerceSyncResult
    {
        public bool Success { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int? WooCommerceId { get; set; }
        public string Action { get; set; } = string.Empty; // "created" or "updated"
        public string? Error { get; set; }
    }

    public class WooCommerceSyncRes
    {
        public string Message { get; set; } = string.Empty;
        public List<WooCommerceSyncResult> Success { get; set; } = new();
        public List<WooCommerceSyncResult> Failed { get; set; } = new();
    }

    public class WooCommerceCategorySyncRes
    {
        public int CategoryId { get; set; }
        public int? WooCommerceId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

