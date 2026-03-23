using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class GeorgeDBContextBase : DbContext
{
    public GeorgeDBContextBase(DbContextOptions<GeorgeDBContextBase> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Account { get; set; }

    public virtual DbSet<AccountMedia> AccountMedia { get; set; }

    public virtual DbSet<AccountNotificationSettings> AccountNotificationSettings { get; set; }

    public virtual DbSet<AccountStatus> AccountStatus { get; set; }

    public virtual DbSet<AccountWizardStepData> AccountWizardStepData { get; set; }

    public virtual DbSet<Attribute> Attribute { get; set; }

    public virtual DbSet<AttributeValue> AttributeValue { get; set; }

    public virtual DbSet<Brand> Brand { get; set; }

    public virtual DbSet<BusinessType> BusinessType { get; set; }

    public virtual DbSet<BusinessTypeCategory> BusinessTypeCategory { get; set; }

    public virtual DbSet<Category> Category { get; set; }

    public virtual DbSet<ContentOwner> ContentOwner { get; set; }

    public virtual DbSet<GlobalCategory> GlobalCategory { get; set; }

    public virtual DbSet<KioskSettings> KioskSettings { get; set; }

    public virtual DbSet<KioskSettingsHomeImage> KioskSettingsHomeImage { get; set; }

    public virtual DbSet<Media> Media { get; set; }

    public virtual DbSet<MediaType> MediaType { get; set; }

    public virtual DbSet<Order> Order { get; set; }

    public virtual DbSet<OrderItem> OrderItem { get; set; }

    public virtual DbSet<Product> Product { get; set; }

    public virtual DbSet<ProductCategory> ProductCategory { get; set; }

    public virtual DbSet<ProductImage> ProductImage { get; set; }

    public virtual DbSet<ProductOption> ProductOption { get; set; }

    public virtual DbSet<ProductOptionValue> ProductOptionValue { get; set; }

    public virtual DbSet<ProductStatus> ProductStatus { get; set; }

    public virtual DbSet<ProductVariant> ProductVariant { get; set; }

    public virtual DbSet<ProductVariantOptionValue> ProductVariantOptionValue { get; set; }

    public virtual DbSet<Role> Role { get; set; }

    public virtual DbSet<SetupType> SetupType { get; set; }

    public virtual DbSet<ShippingClass> ShippingClass { get; set; }

    public virtual DbSet<Site> Site { get; set; }

    public virtual DbSet<SiteOrderReceptionClosed> SiteOrderReceptionClosed { get; set; }

    public virtual DbSet<PrintJob> PrintJob { get; set; }

    public virtual DbSet<StockManagementType> StockManagementType { get; set; }

    public virtual DbSet<StockStatus> StockStatus { get; set; }

    public virtual DbSet<Supplier> Supplier { get; set; }

    public virtual DbSet<SystemConfiguration> SystemConfiguration { get; set; }

    public virtual DbSet<Tag> Tag { get; set; }

    public virtual DbSet<TemplateAttribute> TemplateAttribute { get; set; }

    public virtual DbSet<TemplateAttributeValue> TemplateAttributeValue { get; set; }

    public virtual DbSet<TemplateProduct> TemplateProduct { get; set; }

    public virtual DbSet<TemplateProductCategory> TemplateProductCategory { get; set; }

    public virtual DbSet<TemplateProductImage> TemplateProductImage { get; set; }

    public virtual DbSet<TemplateProductOption> TemplateProductOption { get; set; }

    public virtual DbSet<TemplateProductOptionValue> TemplateProductOptionValue { get; set; }

    public virtual DbSet<TemplateProductVariant> TemplateProductVariant { get; set; }

    public virtual DbSet<TemplateProductVariantOptionValue> TemplateProductVariantOptionValue { get; set; }

    public virtual DbSet<Unit> Unit { get; set; }

    public virtual DbSet<UnitWeightMode> UnitWeightMode { get; set; }

    public virtual DbSet<User> User { get; set; }

    public virtual DbSet<UserPreference> UserPreference { get; set; }

    public virtual DbSet<UserStatus> UserStatus { get; set; }

    public virtual DbSet<Visibility> Visibility { get; set; }

    public virtual DbSet<WeightConfig> WeightConfig { get; set; }

    public virtual DbSet<WizardStatus> WizardStatus { get; set; }

    public virtual DbSet<WizardType> WizardType { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.ContentOwnerId).HasDefaultValue(1);
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.ContentOwner).WithMany(p => p.Account).HasConstraintName("FK_Account_ContentOwner");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.AccountCreationUser).HasConstraintName("FK_Account_CreationUser");

            entity.HasOne(d => d.Manager).WithMany(p => p.AccountManager).HasConstraintName("FK_Account_Manager");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.Account).HasConstraintName("FK_Account_Status");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.AccountUpdateUser).HasConstraintName("FK_Account_UpdateUser");

            entity.HasOne(d => d.WizardStatus).WithMany(p => p.Account).HasConstraintName("FK_Account_WizardStatus");

            entity.HasOne(d => d.WizardType).WithMany(p => p.Account).HasConstraintName("FK_Account_WizardType");
        });

        modelBuilder.Entity<AccountMedia>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(d => d.Account).WithMany(p => p.AccountMedia)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountMedia_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.AccountMedia).HasConstraintName("FK_AccountMedia_CreationUser");

            entity.HasOne(d => d.Media).WithMany(p => p.AccountMedia)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountMedia_Media");

            entity.HasOne(d => d.Site).WithMany(p => p.AccountMedia).HasConstraintName("FK_AccountMedia_Site");
        });

        modelBuilder.Entity<AccountNotificationSettings>(entity =>
        {
            entity.Property(e => e.AfterDeliveryTriggerAfterValue).HasDefaultValue(1);
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NewOrderManagerReminderBeforeDeliveryMinutes).HasDefaultValue(60);
            entity.Property(e => e.NewOrderManagerReminderNoTreatmentMinutes).HasDefaultValue(15);
            entity.Property(e => e.NewOrderManagerSoundEnabled).HasDefaultValue(true);
            entity.Property(e => e.NewOrderManagerSoundTriggerKiosk).HasDefaultValue(true);
            entity.Property(e => e.NewOrderManagerSoundTriggerWebsite).HasDefaultValue(true);
            entity.Property(e => e.OrderNotPickedUpMinutesAfterScheduledPickup).HasDefaultValue(30);

            entity.HasOne(d => d.Account).WithOne(p => p.AccountNotificationSettings).HasConstraintName("FK_AccountNotificationSettings_Account");
        });

        modelBuilder.Entity<AccountWizardStepData>(entity =>
        {
            entity.HasOne(d => d.Account).WithMany(p => p.AccountWizardStepData).HasConstraintName("FK_AccountWizardStepData_Account");

            entity.HasOne(d => d.Site).WithMany(p => p.AccountWizardStepData).HasConstraintName("FK_AccountWizardStepData_Site");
        });

        modelBuilder.Entity<Attribute>(entity =>
        {
            entity.HasIndex(e => new { e.SiteId, e.Name }, "UX_Attribute_SiteId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.AttributeCreationUser).HasConstraintName("FK_Attribute_CreationUser");

            entity.HasOne(d => d.Site).WithMany(p => p.Attribute)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attribute_Site");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.AttributeUpdateUser).HasConstraintName("FK_Attribute_UpdateUser");
        });

        modelBuilder.Entity<AttributeValue>(entity =>
        {
            entity.HasOne(d => d.Attribute).WithMany(p => p.AttributeValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttributeValue_Attribute");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Brand_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Brand).HasConstraintName("FK_Brand_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.BrandCreationUser).HasConstraintName("FK_Brand_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.BrandUpdateUser).HasConstraintName("FK_Brand_UpdateUser");
        });

        modelBuilder.Entity<BusinessType>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.BusinessTypeCreationUser).HasConstraintName("FK_BusinessType_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.BusinessTypeUpdateUser).HasConstraintName("FK_BusinessType_UpdateUser");
        });

        modelBuilder.Entity<BusinessTypeCategory>(entity =>
        {
            entity.HasOne(d => d.BusinessType).WithMany(p => p.BusinessTypeCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BTC_BusinessType");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.ParentCategoryId, e.Name }, "UX_Category_Account_Parent_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ShowInKiosk).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.Category).HasConstraintName("FK_Category_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.CategoryCreationUser).HasConstraintName("FK_Category_CreationUser");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory).HasConstraintName("FK_Category_Parent");

            entity.HasOne(d => d.SourceGlobalCategory).WithMany(p => p.Category).HasConstraintName("FK_Category_SourceGlobalCategory");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.CategoryUpdateUser).HasConstraintName("FK_Category_UpdateUser");

            entity.HasMany(d => d.Site).WithMany(p => p.Category)
                .UsingEntity<Dictionary<string, object>>(
                    "CategorySite",
                    r => r.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CategorySite_Site"),
                    l => l.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CategorySite_Category"),
                    j =>
                    {
                        j.HasKey("CategoryId", "SiteId");
                        j.HasIndex(new[] { "SiteId" }, "IX_CategorySite_SiteId");
                    });
        });

        modelBuilder.Entity<GlobalCategory>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.GlobalCategoryCreationUser).HasConstraintName("FK_GlobalCategory_CreationUser");

            entity.HasOne(d => d.ParentGlobalCategory).WithMany(p => p.InverseParentGlobalCategory).HasConstraintName("FK_GlobalCategory_Parent");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.GlobalCategoryUpdateUser).HasConstraintName("FK_GlobalCategory_UpdateUser");

            entity.HasMany(d => d.BusinessType).WithMany(p => p.GlobalCategory)
                .UsingEntity<Dictionary<string, object>>(
                    "GlobalCategoryBusinessType",
                    r => r.HasOne<BusinessType>().WithMany()
                        .HasForeignKey("BusinessTypeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_GCBT_BusinessType"),
                    l => l.HasOne<GlobalCategory>().WithMany()
                        .HasForeignKey("GlobalCategoryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_GCBT_GlobalCategory"),
                    j =>
                    {
                        j.HasKey("GlobalCategoryId", "BusinessTypeId");
                    });
        });

        modelBuilder.Entity<KioskSettings>(entity =>
        {
            entity.Property(e => e.AccountId).ValueGeneratedNever();
            entity.Property(e => e.CashAtRegisterEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithOne(p => p.KioskSettings).HasConstraintName("FK_KioskSettings_Account");

            entity.HasOne(d => d.HomeVideoMedia).WithMany(p => p.KioskSettings)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KioskSettings_HomeVideoMedia");
        });

        modelBuilder.Entity<KioskSettingsHomeImage>(entity =>
        {
            entity.HasOne(d => d.Account).WithMany(p => p.KioskSettingsHomeImage).HasConstraintName("FK_KioskSettingsHomeImage_Account");

            entity.HasOne(d => d.Media).WithMany(p => p.KioskSettingsHomeImage).HasConstraintName("FK_KioskSettingsHomeImage_Media");
        });

        modelBuilder.Entity<Media>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Media).HasConstraintName("FK_Media_Account");

            entity.HasOne(d => d.BusinessType).WithMany(p => p.Media).HasConstraintName("FK_Media_BusinessType");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.MediaCreationUser).HasConstraintName("FK_Media_CreationUser");

            entity.HasOne(d => d.Type).WithMany(p => p.Media).HasConstraintName("FK_Media_Type");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.MediaUpdateUser).HasConstraintName("FK_Media_UpdateUser");

            entity.HasMany(d => d.Category).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaCategory_Category"),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaCategory_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "CategoryId");
                    });

            entity.HasMany(d => d.Tag).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaTag_Tag"),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaTag_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "TagId");
                        j.HasIndex(new[] { "TagId" }, "IX_MediaTag_TagId");
                    });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber, "IX_Order_OrderNumber").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.SiteId, e.IsDeleted }, "IX_Order_SiteId_IsDeleted").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("Unpaid");
            entity.Property(e => e.Status).HasDefaultValue("New");

            entity.HasOne(d => d.Account).WithMany(p => p.Order)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Account");

            entity.HasOne(d => d.Site).WithMany(p => p.Order)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Site");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(d => d.Order).WithMany(p => p.OrderItem).HasConstraintName("FK_OrderItem_Order");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            //entity.HasIndex(e => new { e.AccountId, e.Sku }, "UX_Product_AccountId_Sku_NotDeleted")
            //    .IsUnique()
            //    .HasFilter("([IsDeleted]=(0) AND [Sku] IS NOT NULL AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Brand).WithMany(p => p.Product).HasConstraintName("FK_Product_Brand");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.ProductCreationUser).HasConstraintName("FK_Product_CreationUser");

            entity.HasOne(d => d.SetupType).WithMany(p => p.Product).HasConstraintName("FK_Product_SetupType");

            entity.HasOne(d => d.ShippingClass).WithMany(p => p.Product).HasConstraintName("FK_Product_ShippingClass");

            entity.HasOne(d => d.Status).WithMany(p => p.Product).HasConstraintName("FK_Product_Status");

            entity.HasOne(d => d.StockManagementType).WithMany(p => p.Product).HasConstraintName("FK_Product_StockManagementType");

            entity.HasOne(d => d.StockStatus).WithMany(p => p.Product).HasConstraintName("FK_Product_StockStatus");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Product).HasConstraintName("FK_Product_Supplier");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.ProductUpdateUser).HasConstraintName("FK_Product_UpdateUser");

            entity.HasOne(d => d.Visibility).WithMany(p => p.Product).HasConstraintName("FK_Product_Visibility");

            entity.HasOne(d => d.WeightConfig).WithMany(p => p.Product).HasConstraintName("FK_Product_WeightConfig");

            entity.HasMany(d => d.ComplementaryProduct).WithMany(p => p.ProductNavigation)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductComplementary",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("ComplementaryProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductComplementary_ComplementaryProduct"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductComplementary_Product"),
                    j =>
                    {
                        j.HasKey("ProductId", "ComplementaryProductId");
                    });

            entity.HasMany(d => d.Product1).WithMany(p => p.RelatedProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductRelated",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductRelated_Product"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("RelatedProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductRelated_RelatedProduct"),
                    j =>
                    {
                        j.HasKey("ProductId", "RelatedProductId");
                    });

            entity.HasMany(d => d.ProductNavigation).WithMany(p => p.ComplementaryProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductComplementary",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductComplementary_Product"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ComplementaryProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductComplementary_ComplementaryProduct"),
                    j =>
                    {
                        j.HasKey("ProductId", "ComplementaryProductId");
                    });

            entity.HasMany(d => d.RelatedProduct).WithMany(p => p.Product1)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductRelated",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("RelatedProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductRelated_RelatedProduct"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductRelated_Product"),
                    j =>
                    {
                        j.HasKey("ProductId", "RelatedProductId");
                    });

            entity.HasMany(d => d.Site).WithMany(p => p.Product)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductSite",
                    r => r.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductSite_Site"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductSite_Product"),
                    j =>
                    {
                        j.HasKey("ProductId", "SiteId");
                        j.HasIndex(new[] { "SiteId" }, "IX_ProductSite_SiteId");
                    });

            entity.HasMany(d => d.Tag).WithMany(p => p.Product)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTag_Tag"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTag_Product"),
                    j =>
                    {
                        j.HasKey("ProductId", "TagId");
                        j.HasIndex(new[] { "TagId" }, "IX_ProductTag_TagId");
                    });
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasOne(d => d.Category).WithMany(p => p.ProductCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategory_Category");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategory_Product");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasOne(d => d.Media).WithMany(p => p.ProductImage)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductImage_Media");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImage)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImage_Product");
        });

        modelBuilder.Entity<ProductOption>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.ProductOption)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductOption_Product");
        });

        modelBuilder.Entity<ProductOptionValue>(entity =>
        {
            entity.HasOne(d => d.ProductOption).WithMany(p => p.ProductOptionValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductOptionValue_ProductOption");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariant)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVariant_Product");
        });

        modelBuilder.Entity<ProductVariantOptionValue>(entity =>
        {
            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductVariantOptionValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVariantOptionValue_ProductVariant");
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Currency).HasDefaultValue("ILS");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.Site)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Site_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.SiteCreationUser).HasConstraintName("FK_Site_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.SiteUpdateUser).HasConstraintName("FK_Site_UpdateUser");

            entity.HasMany(d => d.BusinessType).WithMany(p => p.Site)
                .UsingEntity<Dictionary<string, object>>(
                    "SiteBusinessType",
                    r => r.HasOne<BusinessType>().WithMany()
                        .HasForeignKey("BusinessTypeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SiteBusinessType_BusinessType"),
                    l => l.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SiteBusinessType_Site"),
                    j =>
                    {
                        j.HasKey("SiteId", "BusinessTypeId");
                    });

            entity.HasMany(d => d.User).WithMany(p => p.Site)
                .UsingEntity<Dictionary<string, object>>(
                    "SiteUser",
                    r => r.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SiteUser_User"),
                    l => l.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_SiteUser_Site"),
                    j =>
                    {
                        j.HasKey("SiteId", "UserId");
                    });
        });

        modelBuilder.Entity<SiteOrderReceptionClosed>(entity =>
        {
            entity.HasOne(d => d.Site).WithMany(p => p.SiteOrderReceptionClosed).HasConstraintName("FK_SiteOrderReceptionClosed_Site");
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.JobType).HasMaxLength(50);
            entity.Property(e => e.Trigger).HasMaxLength(80);
            entity.Property(e => e.ClientSource).HasMaxLength(80);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.AgentId).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.HasOne(d => d.Site).WithMany().HasForeignKey(d => d.SiteId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_PrintJob_Site");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Supplier_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Supplier).HasConstraintName("FK_Supplier_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.SupplierCreationUser).HasConstraintName("FK_Supplier_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.SupplierUpdateUser).HasConstraintName("FK_Supplier_UpdateUser");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Tag_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Tag).HasConstraintName("FK_Tag_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TagCreationUser).HasConstraintName("FK_Tag_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TagUpdateUser).HasConstraintName("FK_Tag_UpdateUser");
        });

        modelBuilder.Entity<TemplateAttribute>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TemplateAttributeCreationUser).HasConstraintName("FK_TemplateAttribute_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TemplateAttributeUpdateUser).HasConstraintName("FK_TemplateAttribute_UpdateUser");

            entity.HasMany(d => d.Site).WithMany(p => p.TemplateAttribute)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateAttributeSite",
                    r => r.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TAS_Site"),
                    l => l.HasOne<TemplateAttribute>().WithMany()
                        .HasForeignKey("TemplateAttributeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TAS_TemplateAttribute"),
                    j =>
                    {
                        j.HasKey("TemplateAttributeId", "SiteId");
                    });
        });

        modelBuilder.Entity<TemplateAttributeValue>(entity =>
        {
            entity.HasOne(d => d.TemplateAttribute).WithMany(p => p.TemplateAttributeValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateAttributeValue_TemplateAttribute");
        });

        modelBuilder.Entity<TemplateProduct>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Brand).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_Brand");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TemplateProductCreationUser).HasConstraintName("FK_TemplateProduct_CreationUser");

            entity.HasOne(d => d.SetupType).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_SetupType");

            entity.HasOne(d => d.ShippingClass).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_ShippingClass");

            entity.HasOne(d => d.Status).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_Status");

            entity.HasOne(d => d.StockManagementType).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_StockManagementType");

            entity.HasOne(d => d.StockStatus).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_StockStatus");

            entity.HasOne(d => d.Supplier).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_Supplier");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TemplateProductUpdateUser).HasConstraintName("FK_TemplateProduct_UpdateUser");

            entity.HasOne(d => d.Visibility).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_Visibility");

            entity.HasOne(d => d.WeightConfig).WithMany(p => p.TemplateProduct).HasConstraintName("FK_TemplateProduct_WeightConfig");

            entity.HasMany(d => d.ComplementaryTemplateProduct).WithMany(p => p.TemplateProductNavigation)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductComplementary",
                    r => r.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("ComplementaryTemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPComplementary_ComplementaryTemplateProduct"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPComplementary_TemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "ComplementaryTemplateProductId");
                    });

            entity.HasMany(d => d.RelatedTemplateProduct).WithMany(p => p.TemplateProduct1)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductRelated",
                    r => r.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("RelatedTemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPR_RelatedTemplateProduct"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPR_TemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "RelatedTemplateProductId");
                    });

            entity.HasMany(d => d.Site).WithMany(p => p.TemplateProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductSite",
                    r => r.HasOne<Site>().WithMany()
                        .HasForeignKey("SiteId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPS_Site"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPS_TemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "SiteId");
                    });

            entity.HasMany(d => d.Tag).WithMany(p => p.TemplateProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPT_Tag"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPT_TemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "TagId");
                    });

            entity.HasMany(d => d.TemplateProduct1).WithMany(p => p.RelatedTemplateProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductRelated",
                    r => r.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPR_TemplateProduct"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("RelatedTemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPR_RelatedTemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "RelatedTemplateProductId");
                    });

            entity.HasMany(d => d.TemplateProductNavigation).WithMany(p => p.ComplementaryTemplateProduct)
                .UsingEntity<Dictionary<string, object>>(
                    "TemplateProductComplementary",
                    r => r.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("TemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPComplementary_TemplateProduct"),
                    l => l.HasOne<TemplateProduct>().WithMany()
                        .HasForeignKey("ComplementaryTemplateProductId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_TPComplementary_ComplementaryTemplateProduct"),
                    j =>
                    {
                        j.HasKey("TemplateProductId", "ComplementaryTemplateProductId");
                    });
        });

        modelBuilder.Entity<TemplateProductCategory>(entity =>
        {
            entity.HasOne(d => d.GlobalCategory).WithMany(p => p.TemplateProductCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPC_GlobalCategory");

            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPC_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductImage>(entity =>
        {
            entity.HasOne(d => d.Media).WithMany(p => p.TemplateProductImage)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TemplateProductImage_Media");

            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductImage)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductImage_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductOption>(entity =>
        {
            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductOption)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductOption_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductOptionValue>(entity =>
        {
            entity.HasOne(d => d.TemplateProductOption).WithMany(p => p.TemplateProductOptionValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductOptionValue_TemplateProductOption");
        });

        modelBuilder.Entity<TemplateProductVariant>(entity =>
        {
            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductVariant)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductVariant_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductVariantOptionValue>(entity =>
        {
            entity.HasOne(d => d.TemplateProductVariant).WithMany(p => p.TemplateProductVariantOptionValue)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPVOV_TemplateProductVariant");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasComputedColumnSql("(([FirstName]+N' ')+[LastName])", true);
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Account).WithMany(p => p.User).HasConstraintName("FK_User_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.InverseCreationUser).HasConstraintName("FK_User_CreationUser");

            entity.HasOne(d => d.Role).WithMany(p => p.User)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Role");

            entity.HasOne(d => d.Status).WithMany(p => p.User)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Status");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.InverseUpdateUser).HasConstraintName("FK_User_UpdateUser");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.HasOne(d => d.User).WithOne(p => p.UserPreference).HasConstraintName("FK_UserPreference_User");
        });

        modelBuilder.Entity<WeightConfig>(entity =>
        {
            entity.HasOne(d => d.Unit).WithMany(p => p.WeightConfig).HasConstraintName("FK_WeightConfig_Unit");

            entity.HasOne(d => d.UnitWeightMode).WithMany(p => p.WeightConfig).HasConstraintName("FK_WeightConfig_UnitWeightMode");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
