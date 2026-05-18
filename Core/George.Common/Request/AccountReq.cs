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
        /// <summary>Wizard type: "all_sites", "per_site", or "none" (no client-facing wizard).</summary>
        public string? WizardType { get; set; }

        public string? LogoUrl { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }

        public KioskSettingsReq? KioskSettings { get; set; }

        /// <summary>Sprint 2: Notification settings (התראות). When provided, replaces existing.</summary>
        public NotificationSettingsReq? NotificationSettings { get; set; }

        /// <summary>Default low-stock threshold for weighted (kg-style) products. Null = keep existing.</summary>
        public decimal? DefaultLowStockThresholdWeighted { get; set; }

        /// <summary>Default low-stock threshold for unit-count products. Null = keep existing.</summary>
        public decimal? DefaultLowStockThresholdUnits { get; set; }

        /// <summary>Default duration in days for the &quot;חדש&quot; product label (placeholder / suggested end date). Null = keep existing.</summary>
        public int? DefaultNewLabelDays { get; set; }
    }

    /// <summary>Sprint 2: Notification settings request (relational, no JSON).</summary>
    public class NotificationSettingsReq
    {
        public NewOrderSettingsReq? NewOrder { get; set; }
        public OrderReadySettingsReq? OrderReady { get; set; }
        public OrderNotPickedUpSettingsReq? OrderNotPickedUp { get; set; }
        public AfterDeliverySettingsReq? AfterDelivery { get; set; }
        public PaymentNotificationSettingsReq? Payments { get; set; }
    }

    public class PaymentNotificationSettingsReq
    {
        public string? CustomerMessageInvoice { get; set; }
        public string? CustomerMessageRefund { get; set; }
        public string? CustomerMessagePaymentLink { get; set; }
    }

    public class NewOrderSoundTriggerSourcesReq
    {
        public bool? Website { get; set; }
        public bool? Kiosk { get; set; }
        public bool? Whatsapp { get; set; }
        public bool? Phone { get; set; }
    }

    public class NewOrderSettingsReq
    {
        public bool? ManagerSoundEnabled { get; set; }
        public string? ManagerSoundKey { get; set; }
        public NewOrderSoundTriggerSourcesReq? ManagerSoundTriggerSources { get; set; }
        public string? ManagerMessageChannel { get; set; }
        public string? ManagerPhoneNumbers { get; set; }
        public string? ManagerMessageTemplate { get; set; }
        public bool? ManagerReminderBeforeDeliveryEnabled { get; set; }
        public int? ManagerReminderBeforeDeliveryMinutes { get; set; }
        public bool? ManagerReminderNoTreatmentEnabled { get; set; }
        public int? ManagerReminderNoTreatmentMinutes { get; set; }
        public string? ManagerReminderNoTreatmentSoundKey { get; set; }
        public string? CustomerChannel { get; set; }
        public string? CustomerMessageShipping { get; set; }
        public string? CustomerMessagePickup { get; set; }
        public string? CustomerMessageKiosk { get; set; }
        public bool? CustomerSmsOnPhoneOrderEnabled { get; set; }
        public string? CustomerMessagePhoneOrder { get; set; }
    }

    public class OrderReadySettingsReq
    {
        public bool? ManagerNotifyEnabled { get; set; }
        public string? CustomerChannel { get; set; }
        public string? CustomerMessageShipping { get; set; }
        public string? CustomerMessagePickup { get; set; }
        public string? CustomerMessageKiosk { get; set; }
    }

    public class OrderNotPickedUpSettingsReq
    {
        public bool? ManagerNotifyEnabled { get; set; }
        public bool? AutoReminderEnabled { get; set; }
        public int? MinutesAfterScheduledPickup { get; set; }
        public string? CustomerMessageTemplate { get; set; }
    }

    public class AfterDeliverySettingsReq
    {
        public bool? Enabled { get; set; }
        public string? TriggerType { get; set; }
        public int? TriggerAfterValue { get; set; }
        public string? TriggerAfterUnit { get; set; }
        public string? CustomerMessageTemplate { get; set; }
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
        /// <summary>Show "הזמנה חוזרת" (Repeat Order) button in kiosk (default false).</summary>
        public bool ShowDuplicateOrderButton { get; set; } = false;
        /// <summary>Show products that are out of stock in kiosk categories/search (default false).</summary>
        public bool ShowOutOfStockProducts { get; set; } = false;
        /// <summary>When showing out-of-stock products, place them at the bottom of category/search lists (default false).</summary>
        public bool ShowOutOfStockAtBottom { get; set; } = false;
        /// <summary>Enable POS products (upsell) step; when false, cart goes directly to payment (default true).</summary>
        public bool PosProductsEnabled { get; set; } = true;
        /// <summary>Button text: "To payment / View order" (cart header).</summary>
        public string? ButtonTextToPaymentOrViewOrder { get; set; }
        /// <summary>Button text: "To payment" (cart footer).</summary>
        public string? ButtonTextCartToPayment { get; set; }
        /// <summary>Button text: "Continue to payment" (POS products → checkout).</summary>
        public string? ButtonTextUpsellContinueToPayment { get; set; }
        /// <summary>Seconds of inactivity before "Are you still there?" popup (default 60).</summary>
        public int? InactivityPopupSeconds { get; set; }
        /// <summary>When true, privacy policy checkbox on phone screen is checked by default.</summary>
        public bool PrivacyPolicyCheckboxCheckedByDefault { get; set; } = false;
        /// <summary>Privacy policy content (HTML or plain text); shown in panel when user clicks the link.</summary>
        public string? PrivacyPolicyContent { get; set; }
        /// <summary>Product card image aspect ratio: "3:2" or "1:1".</summary>
        public string? ProductImageAspectRatio { get; set; }
    }

    public class UpdateWizardSessionReq
    {
        public int? Step { get; set; }
        public string? Status { get; set; }
        /// <summary>Wizard type: "all_sites" or "per_site".</summary>
        public string? WizardType { get; set; }
        /// <summary>Full wizard session JSON (selectedCategories, selectedProducts, completedProductIds, etc.) so it can be restored when user returns.</summary>
        public string? DataJson { get; set; }
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
        public string? ManagerPhone { get; set; }
        public string? TempPassword { get; set; }

        public string Status { get; set; } = "Active";
        public string WizardStatus { get; set; } = "Not Started";
        public int WizardStep { get; set; } = 1;
        /// <summary>all_sites, per_site, or none (no client-facing wizard).</summary>
        public string WizardType { get; set; } = "all_sites";

        public bool SendInviteToClient { get; set; } = false;
        public bool IsKosherShop { get; set; } = false;
        public bool AllowWeighted { get; set; } = false;
        public bool KioskEnabled { get; set; } = false;
        
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
    }
}
