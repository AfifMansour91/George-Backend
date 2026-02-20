namespace George.Common.Request
{

    public class UpdateAccountCategoriesReq
    {
        public List<AccountCategoryUpdateItem> Items { get; set; } = new();
    }

    //public class AccountProductListFilter
    //{
    //    public long AccountId { get; set; }
    //    public string? Search { get; set; }
    //    public int? BrandId { get; set; }
    //    public int? SupplierId { get; set; }
    //    public string? EditingStatus { get; set; } // NotEdited / Edited / Published
    //    public bool? IsEnabled { get; set; }
    //    public long? AccountCategoryId { get; set; }
    //}

    public class UpdateAccountProductReq
    {
        public string? Title { get; set; }
        public string? ShortDescription { get; set; }
        public string? DescriptionHtml { get; set; }

        public bool? IsKosher { get; set; }
        public int? KosherStatusId { get; set; }

        public int? WeightPricingModelId { get; set; }
        public bool? ShowPricePer100g { get; set; }
        public decimal? BaseUnitPrice { get; set; }
        public int? BaseUnitId { get; set; }
        public decimal? BaseWeightGrams { get; set; }
        public decimal? WeightStepGrams { get; set; }

        public decimal? StockQuantity { get; set; }

        public bool? IsEnabled { get; set; }
        public string? EditingStatus { get; set; } // e.g. "Edited", "Published"

        public List<UpdateAccountProductVariantReq>? Variants { get; set; }
    }

    public class UpdateAccountProductVariantReq
    {
        public long Id { get; set; }
        public string? VariantSku { get; set; }
        public string? VariantTitle { get; set; }
        public decimal? Price { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool? IsEnabled { get; set; }
    }



    public class AccountCategoryUpdateItem
    {
        public long AccountCategoryId { get; set; }

        public bool CustomNameSet { get; set; }
        public string? CustomName { get; set; }

        public bool IsEnabledSet { get; set; }
        public bool IsEnabled { get; set; }

        public bool ParentAccountCategoryIdSet { get; set; }
        public long? ParentAccountCategoryId { get; set; }

        public bool SortOrderSet { get; set; }
        public int SortOrder { get; set; }
    }


    public class AccountProductListFilter
    {
        public string? Search { get; set; }
        public int? BrandId { get; set; }
        public int? SupplierId { get; set; }
        public bool? IsEnabled { get; set; }
        public string? EditingStatus { get; set; }
        public long? CategoryAccountCategoryId { get; set; }
    }

    public class AccountProductListRow
    {
        public long AccountProductId { get; set; }
        public string Title { get; set; } = default!;
        public string? Sku { get; set; }
        public decimal? Price { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool IsEnabled { get; set; }
        public string EditingStatus { get; set; } = default!;
        public string? PrimaryImageUrl { get; set; }
        public string? BrandName { get; set; }
        public string? SupplierName { get; set; }
    }

    // Update model for step 4 editor
    public class AccountProductUpdateModel
    {
        public string? Title { get; set; }
        public string? ShortDescription { get; set; }
        public string? DescriptionHtml { get; set; }

        public bool? IsKosher { get; set; }
        public int? KosherStatusId { get; set; }

        public int? WeightPricingModelId { get; set; }
        public bool? ShowPricePer100g { get; set; }
        public decimal? BaseUnitPrice { get; set; }
        public int? BaseUnitId { get; set; }
        public decimal? BaseWeightGrams { get; set; }
        public decimal? WeightStepGrams { get; set; }

        public decimal? StockQuantity { get; set; }
        public string? Sku { get; set; }

        public string? EditingStatus { get; set; } // NotEdited / Edited / Published
        public string? Notes { get; set; }

        public List<VariantPatchModel>? Variants { get; set; }
    }

    public class VariantPatchModel
    {
        public long Id { get; set; }
        public string? VariantTitle { get; set; }
        public decimal? Price { get; set; }
        public decimal? StockQuantity { get; set; }
        public decimal? WeightGrams { get; set; }
        public bool? IsEnabled { get; set; }
        public int? SortOrder { get; set; }
    }


    public class CatalogProductListFilter
    {
        public string? Search { get; set; }
        public int? BrandId { get; set; }
        public int? SupplierId { get; set; }
        public int? BusinessTypeId { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class CatalogListRow
    {
        public long ProductTemplateId { get; set; }
        public string Title { get; set; } = default!;
        public string? Sku { get; set; }
        public decimal? BaseUnitPrice { get; set; }
        public bool IsActive { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public string? BrandName { get; set; }
        public string? SupplierName { get; set; }
    }

    public class ProductTemplatePatchModel
    {
        public bool SkuSet { get; set; }
        public string? Sku { get; set; }

        public bool TitleSet { get; set; }
        public string? Title { get; set; }

        public bool ShortDescriptionSet { get; set; }
        public string? ShortDescription { get; set; }

        public bool DescriptionHtmlSet { get; set; }
        public string? DescriptionHtml { get; set; }

        public bool ProductTypeIdSet { get; set; }
        public int? ProductTypeId { get; set; }

        public bool BrandIdSet { get; set; }
        public int? BrandId { get; set; }

        public bool SupplierIdSet { get; set; }
        public int? SupplierId { get; set; }

        public bool IsKosherDefaultSet { get; set; }
        public bool? IsKosherDefault { get; set; }

        public bool KosherStatusIdSet { get; set; }
        public int? KosherStatusId { get; set; }

        public bool WeightPricingModelIdSet { get; set; }
        public int? WeightPricingModelId { get; set; }

        public bool ShowPricePer100gSet { get; set; }
        public bool? ShowPricePer100g { get; set; }

        public bool BaseUnitPriceSet { get; set; }
        public decimal? BaseUnitPrice { get; set; }

        public bool BaseUnitIdSet { get; set; }
        public int? BaseUnitId { get; set; }

        public bool BaseWeightGramsSet { get; set; }
        public decimal? BaseWeightGrams { get; set; }

        public bool IsActiveSet { get; set; }
        public bool? IsActive { get; set; }
    }

    //public class UpdateGlobalCategoryReq
    //{
    //    public string? Name { get; set; }
    //    public string? Slug { get; set; }
    //    public int? SortOrder { get; set; }
    //    public bool? IsActive { get; set; }
    //    // future: ParentCategoryId, etc.
    //}

    //public class CreateGlobalCategoryReq
    //{
    //    public string Name { get; set; } = default!;
    //    public string? Slug { get; set; }
    //    public int? SortOrder { get; set; }
    //    public bool? IsActive { get; set; }
    //    public int? ParentCategoryId { get; set; }
    //}



    public class UpdateAccountReq
    {
        public string Name { get; set; } = default!;
        public bool IsActive { get; set; }
        public bool IsKosherShop { get; set; }
        public bool AllowWeighted { get; set; }
        public bool KioskEnabled { get; set; }
        public int? WizardStep { get; set; }
        public string? WizardStatus { get; set; }
        
        public string? LogoUrl { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }

        public KioskSettingsReq? KioskSettings { get; set; }
    }

    public class KioskSettingsReq
    {
        public string? KioskLogoUrl { get; set; }
        public string? HeaderBgColor { get; set; }
        public string? HomeBgType { get; set; }
        /// <summary>Media ID for home background video (FK to Media).</summary>
        public int? HomeVideoMediaId { get; set; }
        /// <summary>Media IDs for rotating home images (FKs to Media), in display order.</summary>
        public List<int>? HomeImageMediaIds { get; set; }
        public int? HomeImageIntervalSeconds { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? PosProductsTitle { get; set; }
        /// <summary>"upsells" | "category" | "combined"</summary>
        public string? PosProductsType { get; set; }
        public int? PosProductsCategoryId { get; set; }
        public bool CreditEnabled { get; set; }
        public bool CashAtRegisterEnabled { get; set; }
    }

    public class UpdateWizardSessionReq
    {
        public int? Step { get; set; }
        public string? Status { get; set; }
    }


    public class AccountProductUpdateReq
    {
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

        public string? EditingStatus { get; set; } // "Edited", "Published", etc.
        public string? Notes { get; set; }

        public List<AccountProductVariantReq>? Variants { get; set; }
    }

    public class AccountProductVariantReq
    {
        public long Id { get; set; } // existing variant id
        public string? VariantTitle { get; set; }
        public decimal? Price { get; set; }
        public decimal? StockQuantity { get; set; }
        public decimal? WeightGrams { get; set; }
        public bool? IsEnabled { get; set; }
        public int? SortOrder { get; set; }
    }


    public class CreateCatalogProductReq
    {
        public string? Sku { get; set; }
        public string Title { get; set; } = default!;
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

        public List<int> CategoryIds { get; set; } = new();
        public List<int> BusinessTypeIds { get; set; } = new();

        // TODO: allow Media, Attributes, SelectableWeights in the request
    }

    public class UpdateCatalogProductReq : CreateCatalogProductReq
    {
        // same structure for now
    }








    ////////////////////////////////////////////////////// New



    public class CreateAccountReq
    {
        public string AccountName { get; set; } = default!;
        public string? AccountDescription { get; set; }
        public string? AccountAddress { get; set; }
        public string? AccountCity { get; set; }
        public string? AccountState { get; set; }
        public string? AccountZip { get; set; }
        public string? AccountPhone { get; set; }
        public string? ContentOwner { get; set; } // "Client" or "Company"

        public string ManagerName { get; set; } = default!;
        public string ManagerEmail { get; set; } = default!;
        public string? TempPassword { get; set; }

        public string Status { get; set; } = "Active";
        public string WizardStatus { get; set; } = "Not Started";
        public int WizardStep { get; set; } = 1;
        public string WizardType { get; set; } = "all_sites";

        public bool SendInviteToClient { get; set; } = false;
        public bool IsKosherShop { get; set; } = false;
        public bool AllowWeighted { get; set; } = false;
        public bool KioskEnabled { get; set; } = false;
        
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
    }
}
