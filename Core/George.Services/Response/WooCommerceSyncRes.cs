namespace George.Services.Response
{
    /// <summary>Progress update during WooCommerce sync (streamed to client).</summary>
    public class WooCommerceSyncProgress
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
    }

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

    public class WooCommerceAttributeSyncRes
    {
        public int AttributeId { get; set; }
        public int? WooCommerceId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class WooCommerceImportEntityCounts
    {
        public int Created { get; set; }
        public int Updated { get; set; }
    }

    public class WooCommerceImportFromWooRes
    {
        public string Message { get; set; } = string.Empty;
        public WooCommerceImportEntityCounts Categories { get; set; } = new();
        public WooCommerceImportEntityCounts Brands { get; set; } = new();
        public WooCommerceImportEntityCounts Products { get; set; } = new();
        public WooCommerceImportEntityCounts Variations { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>Progress during Woo → George catalog import (streamed as NDJSON).</summary>
    public class WooCommerceImportProgress
    {
        /// <summary>High-level step: <c>fetch</c>, <c>categories</c>, <c>products</c>.</summary>
        public string Phase { get; set; } = "";

        /// <summary>Items in the current phase (e.g. total products to import).</summary>
        public int Total { get; set; }

        /// <summary>Items completed in the current phase.</summary>
        public int Completed { get; set; }
    }
}

