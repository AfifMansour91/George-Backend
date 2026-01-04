using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class TemplateProductOptionReq
    {
        [Required]
        public string Name { get; set; } = null!;
        public List<string> Values { get; set; } = new();
    }

    public class TemplateProductVariantReq
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? OptionValues { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public int? StockQuantity { get; set; }
        public string? Sku { get; set; }
        public decimal? Weight { get; set; }
    }

    // Reuse WeightConfigReq from ProductReq

    public class TemplateProductReq
    {
        public string? TemplateId { get; set; }

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

        public List<TemplateProductOptionReq>? ProductOptions { get; set; }

        public List<TemplateProductVariantReq>? Variants { get; set; }

        public string? Status { get; set; }

        public string? Visibility { get; set; }

        public List<int>? CategoryIds { get; set; } // GlobalCategory IDs

        public List<int>? SubcategoryIds { get; set; } // GlobalCategory IDs

        public List<string>? Tags { get; set; }

        public string? Brand { get; set; }

        public string? Supplier { get; set; }

        public bool? IsKosher { get; set; }

        public bool? IsWeighted { get; set; }

        public string? SetupType { get; set; }

        public WeightConfigReq? WeightConfig { get; set; }

        public List<int>? SiteIds { get; set; } // Empty = all sites

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        public string? SourceProductId { get; set; }
    }

    public class CreateTemplateProductReq : TemplateProductReq
    {
    }

    public class UpdateTemplateProductReq : TemplateProductReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

