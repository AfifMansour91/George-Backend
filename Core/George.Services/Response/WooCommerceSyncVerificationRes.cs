namespace George.Services.Response;

/// <summary>Result of verifying WooCommerce sync consistency across sites (no SKU collision, duplicates per site, etc.).</summary>
public class WooCommerceSyncVerificationRes
{
    public string Message { get; set; } = "";
    public bool AllSitesOk { get; set; }
    public List<SiteSyncVerificationReport> Sites { get; set; } = new();
    /// <summary>Raw SKUs that appear in more than one site (before prefix). After using site-prefixed SKU (S{siteId}_) these no longer collide in WooCommerce.</summary>
    public List<string> CrossSiteSkuOverlap { get; set; } = new();
}

/// <summary>Per-site sync verification: product counts and any duplicate SKUs within that site.</summary>
public class SiteSyncVerificationReport
{
    public int SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public int ProductCount { get; set; }
    public int WithSkuCount { get; set; }
    public int WithWooCommerceIdCount { get; set; }
    /// <summary>SKUs that appear more than once in this site (duplicates within site).</summary>
    public List<string> DuplicateSkusInSite { get; set; } = new();
    /// <summary>Whether this site has WooCommerce configured (URL + key + secret).</summary>
    public bool WooCommerceConfigured { get; set; }
}
