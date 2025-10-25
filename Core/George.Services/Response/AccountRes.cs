using George.Data;

namespace George.Services.Response
{
    public class AccountRes
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string? StoreDomain { get; set; }
        public bool IsKosherShop { get; set; }
        public bool AllowWeighted { get; set; }
        public bool IsActive { get; set; }
        public List<IdNamePair> BusinessTypes { get; set; } = new();
        public WizardSessionRes? WizardSession { get; set; }
    }

    public class AccountCategoryRes
    {
        public long AccountCategoryId { get; set; }
        public long AccountId { get; set; }
        public long? ParentAccountCategoryId { get; set; }
        public int SortOrder { get; set; }
        public bool IsEnabled { get; set; }

        public string DisplayName { get; set; } = default!; // CustomName ?? global category name
        public string? CustomName { get; set; }
        public int MasterCategoryId { get; set; }
    }

    public class AccountProductListItemRes
    {
        public long AccountProductId { get; set; }
        public string Title { get; set; } = default!;
        public string? Sku { get; set; }
        public decimal? BaseUnitPrice { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool IsEnabled { get; set; }
        public string EditingStatus { get; set; } = default!;

        public string? PrimaryImageUrl { get; set; }
        public string? CategoryNames { get; set; }

        public bool? IsKosher { get; set; }
        public string? KosherStatusName { get; set; }

        public string? WeightModelCode { get; set; }
    }

    //public class AccountProductDetailRes
    //{
    //    public long AccountProductId { get; set; }
    //    public long AccountId { get; set; }

    //    public string Title { get; set; } = default!;
    //    public string? ShortDescription { get; set; }
    //    public string? DescriptionHtml { get; set; }

    //    public bool? IsKosher { get; set; }
    //    public int? KosherStatusId { get; set; }

    //    public int? WeightPricingModelId { get; set; }
    //    public bool? ShowPricePer100g { get; set; }
    //    public decimal? BaseUnitPrice { get; set; }
    //    public int? BaseUnitId { get; set; }
    //    public decimal? BaseWeightGrams { get; set; }
    //    public decimal? WeightStepGrams { get; set; }

    //    public decimal? StockQuantity { get; set; }

    //    public string EditingStatus { get; set; } = default!;
    //    public bool IsEnabled { get; set; }

    //    public List<AccountProductVariantRes> Variants { get; set; } = new();
    //    public List<AccountProductMediaRes> Media { get; set; } = new();
    //}

    //public class AccountProductVariantRes
    //{
    //    public long Id { get; set; }
    //    public string? VariantSku { get; set; }
    //    public string? VariantTitle { get; set; }
    //    public decimal? Price { get; set; }
    //    public decimal? StockQuantity { get; set; }
    //    public decimal? WeightGrams { get; set; }
    //    public bool IsEnabled { get; set; }
    //}

    //public class AccountProductMediaRes
    //{
    //    public long Id { get; set; }
    //    public string Url { get; set; } = default!;
    //    public bool IsPrimary { get; set; }
    //    public int SortOrder { get; set; }
    //}

    public class CatalogLookupsRes
    {
        public List<IdNamePair> Brands { get; set; } = new();
        public List<IdNamePair> Suppliers { get; set; } = new();
        public List<IdNamePair> KosherStatuses { get; set; } = new();
        public List<IdNamePair> WeightPricingModels { get; set; } = new();
        public List<IdNamePair> Units { get; set; } = new();
        public List<IdNamePair> BusinessTypes { get; set; } = new();
    }

    public class GlobalCategoryNodeRes
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = default!;
        public string? Slug { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateAccountRes
    {
        public long AccountId { get; set; }
        public long WizardSessionId { get; set; }
        public string? InviteToken { get; set; }
    }

    public class WizardSessionRes
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public int Step { get; set; }
        public string Status { get; set; } = default!;
        public string ContentOwner { get; set; } = default!;
        public string? InviteToken { get; set; }
    }

    public class AccountProductToggleRes
    {
        public long AccountProductId { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class AccountProductDetailRes
    {
        public long AccountProductId { get; set; }
        public string? Title { get; set; }
        public string? ShortDescription { get; set; }
        public string? DescriptionHtml { get; set; }

        public string? Sku { get; set; }

        public bool? IsKosher { get; set; }
        public int? KosherStatusId { get; set; }

        public int? WeightPricingModelId { get; set; }
        public bool? ShowPricePer100g { get; set; }

        public decimal? BaseUnitPrice { get; set; }
        public int? BaseUnitId { get; set; }

        public decimal? BaseWeightGrams { get; set; }
        public decimal? WeightStepGrams { get; set; }

        public decimal? StockQuantity { get; set; }

        public string EditingStatus { get; set; } = default!; // NotEdited / Edited / Published

        public List<AccountProductMediaRes> Media { get; set; } = new();
        public List<AccountProductVariantRes> Variants { get; set; } = new();
    }

    public class AccountProductMediaRes
    {
        public long Id { get; set; }
        public string Url { get; set; } = default!;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class AccountProductVariantRes
    {
        public long Id { get; set; }
        public string? VariantTitle { get; set; }
        public decimal? Price { get; set; }
        public decimal? StockQuantity { get; set; }
        public decimal? WeightGrams { get; set; }
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
    }

    public class CatalogProductDetailRes
    {
        public long ProductTemplateId { get; set; }

        public string? Sku { get; set; }
        public string? Title { get; set; }
        public string? ShortDescription { get; set; }
        public string? DescriptionHtml { get; set; }

        public int ProductTypeId { get; set; }
        public int? BrandId { get; set; }
        public int? SupplierId { get; set; }

        public bool IsKosherDefault { get; set; }
        public int? KosherStatusId { get; set; }

        public int? WeightPricingModelId { get; set; }
        public bool ShowPricePer100g { get; set; }

        public decimal? BaseUnitPrice { get; set; }
        public int? BaseUnitId { get; set; }
        public decimal? BaseWeightGrams { get; set; }

        public bool IsActive { get; set; }

        public List<CatalogProductMediaRes> Media { get; set; } = new();
        public List<CatalogProductAttributeRes> Attributes { get; set; } = new();
        public List<CatalogSelectableWeightRes> SelectableWeights { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public List<int> BusinessTypeIds { get; set; } = new();
    }

    public class CatalogProductMediaRes
    {
        public long Id { get; set; }
        public string Url { get; set; } = default!;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class CatalogProductAttributeRes
    {
        public long ProductTemplateAttributeId { get; set; }
        public int AttributeId { get; set; }
        public string AttributeName { get; set; } = default!;
        public bool IsVariantAxis { get; set; }
        public List<CatalogProductAttributeOptionRes> Options { get; set; } = new();
    }

    public class CatalogProductAttributeOptionRes
    {
        public long ProductTemplateAttributeOptionId { get; set; }
        public int AttributeOptionId { get; set; }
        public string Value { get; set; } = default!;
        public int SortOrder { get; set; }
    }

    public class CatalogSelectableWeightRes
    {
        public long Id { get; set; }
        public string Label { get; set; } = default!;
        public decimal WeightGrams { get; set; }
        public int SortOrder { get; set; }
    }
}
