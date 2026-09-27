namespace George.Services.Request
{
    public class WooCommerceSyncReq
    {
        public int SiteId { get; set; }
        public List<int>? ProductIds { get; set; } // If null, sync all products for the site
    }

    public class WooCommerceSyncCategoryReq
    {
        public int CategoryId { get; set; }
        public int SiteId { get; set; }
    }

    public class WooCommerceSyncAttributeReq
    {
        public int AttributeId { get; set; }
        public int SiteId { get; set; }
    }

    /// <summary>Re-push the manual attribute value order (term menu_order) to one site, or to every WooCommerce-configured site.</summary>
    public class WooCommercePushAttributeValueOrderReq
    {
        /// <summary>Target site; ignored when <see cref="AllSites"/> is set.</summary>
        public int? SiteId { get; set; }
        /// <summary>Every site with WooCommerce enabled and credentials (super admin maintenance, e.g. after a plugin update).</summary>
        public bool AllSites { get; set; }
        /// <summary>Also queue the background re-sync of the products using each ordered attribute (variation menu_order). Heavy; off by default.</summary>
        public bool IncludeProducts { get; set; }
    }
}

