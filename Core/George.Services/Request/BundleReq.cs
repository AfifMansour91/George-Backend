using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    /// <summary>One allowed swap of a component slot (<c>id</c> = existing row; absent = create).</summary>
    public class ProductBundleSwapReq
    {
        public int? Id { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public decimal Surcharge { get; set; }
        /// <summary>Own quantity per bundle in the alternative's unit; null / 0 = inherit the slot quantity.</summary>
        public decimal? Qty { get; set; }
        public int? SortOrder { get; set; }
    }

    /// <summary>One component slot of a bundle (<c>id</c> = existing row; absent = create; an existing id left out = soft-delete).</summary>
    public class ProductBundleComponentReq
    {
        public int? Id { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        /// <summary>Per bundle, in the component's own unit (kg for weight products, units otherwise).</summary>
        public decimal Qty { get; set; }
        public int? SortOrder { get; set; }
        public bool Swappable { get; set; }
        public string? Description { get; set; }
        /// <summary>
        /// Weight (kg) of one unit, for a product whose unit weight is chosen from a list (by_unit + "variable") -
        /// the slot then costs price × this weight per unit, like a regular order line. Ignored for other products.
        /// </summary>
        public decimal? UnitWeightKg { get; set; }
        public List<ProductBundleSwapReq>? Swaps { get; set; }
    }

    /// <summary>
    /// <c>ProductReq.Bundle</c>: bundle definition sent with a product whose <c>setupType = "bundle"</c>.
    /// Spec: BUNDLES_SYNC_SPEC.md §3.1 (same shape as the response minus computed/read-only fields).
    /// </summary>
    public class ProductBundleReq
    {
        /// <summary>fixed | sum</summary>
        public string? PricingMode { get; set; }
        public decimal? FixedPrice { get; set; }
        /// <summary>none | percent | fixed</summary>
        public string? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        /// <summary>unavailable | swap</summary>
        public string? OosBehavior { get; set; }
        /// <summary>grid | list</summary>
        public string? Layout { get; set; }
        /// <summary>name_with_components | name_only | line</summary>
        public string? CartDisplay { get; set; }
        /// <summary>bundle | components</summary>
        public string? InvoiceDisplay { get; set; }
        public bool? HidePriceLabels { get; set; }
        public bool? ReweighPrice { get; set; }
        public bool? ShowComponentsInDesc { get; set; }
        public List<ProductBundleComponentReq>? Components { get; set; }
    }

    /// <summary>GET /Bundle query.</summary>
    public class BundleListReq
    {
        public int? SiteId { get; set; }
        /// <summary>Impersonated account (super-admin browsing another account with no site selected). Ignored for account-scoped users.</summary>
        public int? AccountId { get; set; }
        public string? Search { get; set; }
        /// <summary>ProductStatus name filter (active | hidden | draft | archived).</summary>
        public string? Status { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    /// <summary>One slot that differs from the bundle config (a swap) for server pricing.</summary>
    public class BundlePriceComponentReq
    {
        [Required]
        public int ComponentId { get; set; }
        /// <summary>Product actually in the slot.</summary>
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        /// <summary>Surcharge per bundle. Null = the configured swap's surcharge (0 when the product is not a configured swap).</summary>
        public decimal? Surcharge { get; set; }
    }

    /// <summary>POST /Bundle/price - server pricing for the UI (new order, picking). Spec §3.2.</summary>
    public class BundlePriceReq
    {
        public int SiteId { get; set; }
        [Required]
        public int BundleProductId { get; set; }
        public decimal BundleQty { get; set; } = 1m;
        /// <summary>Only slots that differ from the config.</summary>
        public List<BundlePriceComponentReq>? Components { get; set; }
    }
}
