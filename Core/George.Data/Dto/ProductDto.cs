using George.Common;
using George.Common.Utils;
using Newtonsoft.Json;

namespace George.Data.Dto
{
    public class ProductOptionDto
    {
        public string Name { get; set; } = null!;
        public List<string> Values { get; set; } = new();
    }

    public class ProductVariantDto
    {
        /// <summary>Existing ProductVariant id (edit form). Null = match by option values, else create.</summary>
        public int? Id { get; set; }
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? OptionValues { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? Sku { get; set; }
        public decimal? Weight { get; set; }
    }

    public class WeightConfigDto
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
        public OcwsuSoldByLabelKey? SoldByLabel { get; set; }
    }

    public class ProductLookupDto
    {
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public string? StockManagementType { get; set; }
        public string? StockStatus { get; set; }
        public string? ShippingClass { get; set; }
        public string? SetupType { get; set; }
        /// <summary>Account-scoped brand IDs (many-to-many via ProductBrand). Null = leave join unchanged where applicable.</summary>
        public List<int>? BrandIds { get; set; }
        public string? Brand { get; set; }
        public string? Supplier { get; set; }
        public WeightConfigDto? WeightConfig { get; set; }
    }
}


