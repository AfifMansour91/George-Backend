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

    /// <summary>Woo product REST <c>id</c> that appeared more than once in the merged import feed (same id, multiple JSON rows).</summary>
    public class WooCommerceImportFeedDuplicateRow
    {
        public int WooProductId { get; set; }
        public int RowCount { get; set; }
        public string? NameHint { get; set; }
    }

    public class WooCommerceImportFromWooRes
    {
        public string Message { get; set; } = string.Empty;
        public WooCommerceImportEntityCounts Categories { get; set; } = new();
        public WooCommerceImportEntityCounts Brands { get; set; } = new();
        public WooCommerceImportEntityCounts Attributes { get; set; } = new();
        public WooCommerceImportEntityCounts Products { get; set; } = new();
        public WooCommerceImportEntityCounts Variations { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        /// <summary>Woo REST rows with valid product id after merging <c>status=any</c> + <c>trash</c>, before de-duplicating repeated ids in the feed.</summary>
        public int WooProductFeedRowCount { get; set; }

        /// <summary>Distinct Woo product REST ids actually imported (one local row per id).</summary>
        public int WooProductUniqueIdCount { get; set; }

        /// <summary>Each Woo product id that occurred more than once in the raw feed (empty when there are no duplicate rows).</summary>
        public List<WooCommerceImportFeedDuplicateRow> WooProductFeedDuplicates { get; set; } = new();
    }

    /// <summary>Result of importing or pushing product display order only (menu_order ↔ DisplayOrder).</summary>
    public class WooCommerceProductOrderSyncRes
    {
        public string Message { get; set; } = string.Empty;
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    /// <summary>
    /// Status of the background menu_order push for a site (the push endpoint returns immediately; the UI polls this).
    /// In-memory only — resets to <c>idle</c> on app restart.
    /// </summary>
    public class ProductOrderPushStatusRes
    {
        /// <summary><c>idle</c> | <c>running</c> | <c>done</c> | <c>failed</c>.</summary>
        public string State { get; set; } = "idle";
        /// <summary>Products queued for the current/last push.</summary>
        public int TotalCount { get; set; }
        public int UpdatedCount { get; set; }
        /// <summary>Products whose Woo id no longer exists on the store (stale/deleted); listed in the server log.</summary>
        public int SkippedCount { get; set; }
        public string? Error { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
    }

    /// <summary>Read-only row for comparing product sort order (George vs WooCommerce). No DB writes.</summary>
    public class ProductOrderPreviewItem
    {
        public int ListIndex { get; set; }
        public int? GeorgeProductId { get; set; }
        public int? WooCommerceProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? DisplayOrder { get; set; }
        public int? MenuOrder { get; set; }
        public string? Sku { get; set; }
        public string? Status { get; set; }
    }

    public class ProductOrderPreviewRes
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<ProductOrderPreviewItem> Products { get; set; } = new();
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

