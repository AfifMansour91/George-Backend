using George.Common;
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
        public int? StockQuantity { get; set; }
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
        public int? StockQuantity { get; set; }
        public string? StockStatus { get; set; }
        public decimal? Weight { get; set; }
        public string? ShippingClass { get; set; }
        public List<ProductOptionReq>? ProductOptions { get; set; }
        public List<ProductVariantReq>? Variants { get; set; }
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public List<int>? CategoryIds { get; set; }
        public List<int>? SubcategoryIds { get; set; }
        public List<string>? Tags { get; set; }
        public string? Brand { get; set; }
        public string? Supplier { get; set; }
        public bool? IsKosher { get; set; }
        public bool? IsWeighted { get; set; }
        public string? SetupType { get; set; }
        public WeightConfigReq? WeightConfig { get; set; }
        public List<int>? SiteIds { get; set; } // Empty = all sites
        public int? AccountId { get; set; }
    }

    public class CreateProductReq : ProductReq
    {
    }

    public class UpdateProductReq : ProductReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

