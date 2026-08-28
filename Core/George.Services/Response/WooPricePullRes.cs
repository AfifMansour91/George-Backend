namespace George.Services.Response
{
    /// <summary>Result of an external price pull (Woo → George) for one site (Site.ExternalPriceManagement).</summary>
    public class WooPricePullRes
    {
        public int SiteId { get; set; }

        /// <summary>Products returned by the store's product list.</summary>
        public int WooProductsScanned { get; set; }

        /// <summary>Store products that matched a George product on this site (per-site Woo-id map or legacy column).</summary>
        public int ProductsMatched { get; set; }

        /// <summary>George products whose price actually changed.</summary>
        public int ProductsUpdated { get; set; }

        /// <summary>George variants whose price actually changed.</summary>
        public int VariantsUpdated { get; set; }

        /// <summary>Store products with no linked George product (never synced / not on this site) - skipped.</summary>
        public int UnmatchedWooProducts { get; set; }

        /// <summary>Products that failed to apply (see server log).</summary>
        public int Errors { get; set; }

        public long DurationMs { get; set; }

        public string? Message { get; set; }
    }
}
