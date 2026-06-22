using George.Common;
using George.Common.Utils;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class ProductOptionReq
    {
        [Required]
        public string Name { get; set; } = null!;
        public List<string> Values { get; set; } = new();
    }

    public class ProductVariantReq
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? OptionValues { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? Sku { get; set; }
        public decimal? Weight { get; set; }
    }

    public class WeightConfigReq
    {
        public string? Unit { get; set; }
        public string? StartWeight { get; set; }
        public string? Step { get; set; }
        public bool? FixedWeightPerUnit { get; set; }
        public string? UnitWeight { get; set; }
        public string? UnitWeightMode { get; set; }
        public string? WeightOptions { get; set; }
        public bool? WeightByVariant { get; set; }
        public bool? ShowPricePer100g { get; set; }
        public bool? ShowUnitPrice { get; set; }

        [JsonConverter(typeof(OcwsuSoldByLabelKeyJsonConverter))]
        public OcwsuSoldByLabelKey? SoldByLabel { get; set; }
    }

    public class ProductReq
    {
        [Required]
        public string Name { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public List<string>? ImageUrls { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public DateTime? SalePriceStartDate { get; set; }
        public DateTime? SalePriceEndDate { get; set; }
        public decimal? CostPrice { get; set; }
        public string? Sku { get; set; }
        public string? StockManagementType { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? StockStatus { get; set; }
        /// <summary>When StockManagementType is variation: true = track quantity per variation; false = in/out only (default).</summary>
        public bool? VariationStockByQuantity { get; set; }
        public decimal? Weight { get; set; }
        public string? ShippingClass { get; set; }
        public List<ProductOptionReq>? ProductOptions { get; set; }
        public List<ProductVariantReq>? Variants { get; set; }
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public List<int>? CategoryIds { get; set; }
        public List<int>? SubcategoryIds { get; set; }
        public List<string>? Tags { get; set; }
        /// <summary>Account brand IDs (many-to-many). When set (including empty), replaces ProductBrand rows. Omit for partial updates.</summary>
        public List<int>? BrandIds { get; set; }
        public string? Brand { get; set; }
        public string? Supplier { get; set; }
        public bool? IsKosher { get; set; }
        public bool? IsWeighted { get; set; }
        public string? SetupType { get; set; }
        public WeightConfigReq? WeightConfig { get; set; }
        public bool? ShowAsMl { get; set; }
        public string? WeightUnit { get; set; }
        /// <summary>Site assignment. Null or empty = do not change which sites the product is on (update); Woo sync falls back to existing sites on the product.</summary>
        public List<int>? SiteIds { get; set; }

        /// <summary>MultiSite Phase 2: 'network' (default) or 'local'. Omit on update = keep existing.</summary>
        public string? ManagementMode { get; set; }
        /// <summary>When creating a local product: the owning site id.</summary>
        public int? OwnerSiteId { get; set; }

        public int? AccountId { get; set; }
        /// <summary>List sort order (ascending). When omitted on create, defaults to 0 in storage.</summary>
        public int? DisplayOrder { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }

        /// <summary>WooCommerce permalink slug (product <c>slug</c>).</summary>
        public string? Slug { get; set; }

        /// <summary>Related/accessory product IDs (נלווים).</summary>
        public List<int>? RelatedProductIds { get; set; }

        /// <summary>Complementary product IDs (מוצרים משלימים).</summary>
        public List<int>? ComplementaryProductIds { get; set; }

        /// <summary>Per-product low-stock threshold. When set overrides the account default. Null clears the override.</summary>
        public decimal? LowStockThreshold { get; set; }

        /// <summary>קפוא</summary>
        public bool? LabelFrozen { get; set; }

        /// <summary>ללא גלוטן</summary>
        public bool? LabelGlutenFree { get; set; }

        /// <summary>לא כשר</summary>
        public bool? LabelNotKosher { get; set; }

        /// <summary>כשר לפסח</summary>
        public bool? LabelKosherForPassover { get; set; }

        public DateTime? LabelKosherForPassoverEndDate { get; set; }

        /// <summary>חדש</summary>
        public bool? LabelNew { get; set; }

        public DateTime? LabelNewEndDate { get; set; }

        /// <summary>רב מכר</summary>
        public bool? LabelBestseller { get; set; }

        /// <summary>זמינות נמוכה</summary>
        public bool? LabelLowAvailability { get; set; }

        /// <summary>מוכן לבישול</summary>
        public bool? LabelReadyToCook { get; set; }

        /// <summary>טבעי</summary>
        public bool? LabelNatural { get; set; }

        /// <summary>ללא תוספת סוכר</summary>
        public bool? LabelSugarFree { get; set; }

        /// <summary>ללא לקטוז</summary>
        public bool? LabelLactoseFree { get; set; }
    }

    public class CreateProductReq : ProductReq
    {
    }

    public class UpdateProductReq : ProductReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }

        /// <summary>
        /// MultiSite Phase 2: 'all_sites' updates the canonical product and clears per-field overrides on
        /// non-excluded sites; 'selected_site' writes a per-site override only (requires SiteId).
        /// Null/empty = legacy behavior (update canonical directly).
        /// </summary>
        public string? EditScope { get; set; }

        /// <summary>Required when EditScope = 'selected_site'.</summary>
        public int? SiteId { get; set; }
    }

    public class UpdateProductOrderReq
    {
        public List<int> ProductIds { get; set; } = new();
    }
}


