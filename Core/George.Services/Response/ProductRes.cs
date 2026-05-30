namespace George.Services.Response
{
    public class ProductOptionRes
    {
        public string Name { get; set; } = null!;
        public List<string> Values { get; set; } = new();
    }

    public class ProductVariantRes
    {
        public int Id { get; set; }
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? OptionValues { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? Sku { get; set; }
        public decimal? Weight { get; set; }
    }

    public class WeightConfigRes
    {
        public string? Unit { get; set; }
        public string? StartWeight { get; set; }
        public string? Step { get; set; }
        public bool? FixedWeightPerUnit { get; set; }
        public string? UnitWeight { get; set; }
        public string? UnitWeightMode { get; set; } // "average" | "variable" | "by_variant"
        public string? WeightOptions { get; set; }
        public bool? WeightByVariant { get; set; }
        public bool? ShowPricePer100g { get; set; }
        public bool? ShowUnitPrice { get; set; }
    }

    public class ProductRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public string Name { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        /// <summary>Display names for images (same order as ImageUrls). From Media.Name when available.</summary>
        public List<string> ImageNames { get; set; } = new();
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public DateTime? SalePriceStartDate { get; set; }
        public DateTime? SalePriceEndDate { get; set; }
        public decimal? CostPrice { get; set; }
        public string? Sku { get; set; }
        public string? StockManagementType { get; set; } // "quantity" | "status" | "variation"
        public decimal? StockQuantity { get; set; }
        /// <summary>When stock per variation: whether each variation uses quantity (true) or in/out only (false).</summary>
        public bool? VariationStockByQuantity { get; set; }
        public string? StockStatus { get; set; } // "in_stock" | "out_of_stock" | "on_backorder"
        public decimal? Weight { get; set; }
        public string? ShippingClass { get; set; } // "default" | "heavy" | "fragile"
        public List<ProductOptionRes> ProductOptions { get; set; } = new();
        public List<ProductVariantRes> Variants { get; set; } = new();
        public string? Status { get; set; } // "published" | "draft" | "archived"
        public string? Visibility { get; set; } // "public" | "hidden" | "private"
        public List<int> CategoryIds { get; set; } = new();
        public List<int> SubcategoryIds { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        /// <summary>Assigned account brand IDs (ProductBrand join; legacy Product.BrandId included when join is empty).</summary>
        public List<int> BrandIds { get; set; } = new();
        public string? Brand { get; set; }
        public string? Supplier { get; set; }
        public bool? IsKosher { get; set; }
        public bool? IsWeighted { get; set; }
        public string? SetupType { get; set; } // "standard" | "by_unit" | "by_weight" | "by_unit_and_weight"
        public WeightConfigRes? WeightConfig { get; set; }
        public bool? ShowAsMl { get; set; }
        public string? WeightUnit { get; set; } // "kg" | "g" | "ml"
        public List<int> SiteIds { get; set; } = new(); // Empty = all sites
        public int? AccountId { get; set; }
        public int? DisplayOrder { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }

        /// <summary>WooCommerce permalink slug.</summary>
        public string? Slug { get; set; }

        /// <summary>Related/accessory product IDs (נלווים).</summary>
        public List<int> RelatedProductIds { get; set; } = new();

        /// <summary>Complementary product IDs (מוצרים משלימים).</summary>
        public List<int> ComplementaryProductIds { get; set; } = new();

        /// <summary>Per-product low-stock threshold. When set (> 0) overrides the account default for low-stock coloring and filtering.</summary>
        public decimal? LowStockThreshold { get; set; }

        public bool LabelFrozen { get; set; }

        public bool LabelGlutenFree { get; set; }

        public bool LabelNotKosher { get; set; }

        public bool LabelKosherForPassover { get; set; }

        public DateTime? LabelKosherForPassoverEndDate { get; set; }

        public bool LabelNew { get; set; }

        public DateTime? LabelNewEndDate { get; set; }

        public bool LabelBestseller { get; set; }

        public bool LabelLowAvailability { get; set; }

        public bool LabelReadyToCook { get; set; }

        public bool LabelNatural { get; set; }

        public bool LabelSugarFree { get; set; }

        public bool LabelLactoseFree { get; set; }
    }
}

