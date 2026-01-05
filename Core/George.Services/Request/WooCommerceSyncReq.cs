namespace George.Services.Request
{
    public class WooCommerceSyncReq
    {
        public int SiteId { get; set; }
        public List<int>? ProductIds { get; set; } // If null, sync all products for the site
    }
}

