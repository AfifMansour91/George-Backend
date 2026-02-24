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

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<KioskSettings> KioskSettings { get; set; }

    public virtual DbSet<KioskSettingsHomeImage> KioskSettingsHomeImages { get; set; }

    public virtual DbSet<AccountNotificationSettings> AccountNotificationSettings { get; set; }

    public virtual DbSet<AccountMedia> AccountMedia { get; set; }

    public virtual DbSet<AccountStatus> AccountStatuses { get; set; }

    public virtual DbSet<Attribute> Attributes { get; set; }

    public virtual DbSet<AttributeValue> AttributeValues { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<BusinessType> BusinessTypes { get; set; }

    public virtual DbSet<BusinessTypeCategory> BusinessTypeCategories { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ContentOwner> ContentOwners { get; set; }

    public virtual DbSet<GlobalCategory> GlobalCategories { get; set; }

    public virtual DbSet<MediaType> MediaTypes { get; set; }

    public virtual DbSet<Medium> Media { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<SiteOrderReceptionClosed> SiteOrderReceptionClosed { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductOption> ProductOptions { get; set; }

    public virtual DbSet<ProductOptionValue> ProductOptionValues { get; set; }

    public virtual DbSet<ProductStatus> ProductStatuses { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<ProductVariantOptionValue> ProductVariantOptionValues { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SetupType> SetupTypes { get; set; }

    public virtual DbSet<ShippingClass> ShippingClasses { get; set; }

    public virtual DbSet<Site> Sites { get; set; }

    public virtual DbSet<StockManagementType> StockManagementTypes { get; set; }

    public virtual DbSet<StockStatus> StockStatuses { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TemplateAttribute> TemplateAttributes { get; set; }

    public virtual DbSet<TemplateAttributeValue> TemplateAttributeValues { get; set; }

    public virtual DbSet<TemplateProduct> TemplateProducts { get; set; }

    public virtual DbSet<TemplateProductCategory> TemplateProductCategories { get; set; }

    public virtual DbSet<TemplateProductImage> TemplateProductImages { get; set; }

    public virtual DbSet<TemplateProductOption> TemplateProductOptions { get; set; }

    public virtual DbSet<TemplateProductOptionValue> TemplateProductOptionValues { get; set; }

    public virtual DbSet<TemplateProductVariant> TemplateProductVariants { get; set; }

    public virtual DbSet<TemplateProductVariantOptionValue> TemplateProductVariantOptionValues { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<UnitWeightMode> UnitWeightModes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPreference> UserPreferences { get; set; }

    public virtual DbSet<UserStatus> UserStatuses { get; set; }

    public virtual DbSet<Visibility> Visibilities { get; set; }

    public virtual DbSet<WeightConfig> WeightConfigs { get; set; }

    public virtual DbSet<WizardStatus> WizardStatuses { get; set; }

    public virtual DbSet<WizardType> WizardTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.ContentOwnerId).HasDefaultValue(1);
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.ContentOwner).WithMany(p => p.Accounts).HasConstraintName("FK_Account_ContentOwner");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.AccountCreationUsers).HasConstraintName("FK_Account_CreationUser");

            entity.HasOne(d => d.Manager).WithMany(p => p.AccountManagers).HasConstraintName("FK_Account_Manager");

            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.Accounts).HasConstraintName("FK_Account_Status");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.AccountUpdateUsers).HasConstraintName("FK_Account_UpdateUser");

            entity.HasOne(d => d.WizardStatus).WithMany(p => p.Accounts).HasConstraintName("FK_Account_WizardStatus");

            entity.HasOne(d => d.WizardType).WithMany(p => p.Accounts).HasConstraintName("FK_Account_WizardType");
        });

        modelBuilder.Entity<KioskSettings>(entity =>
        {
            entity.HasOne(d => d.Account).WithOne(p => p.KioskSettings)
                .HasForeignKey<KioskSettings>(d => d.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.HomeVideoMedia).WithMany(p => p.KioskSettingsHomeVideos)
                .HasForeignKey(d => d.HomeVideoMediaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AccountNotificationSettings>(entity =>
        {
            entity.HasOne(d => d.Account).WithOne(p => p.NotificationSettings)
                .HasForeignKey<AccountNotificationSettings>(d => d.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KioskSettingsHomeImage>(entity =>
        {
            entity.HasOne(d => d.Account).WithMany(p => p.KioskSettingsHomeImages)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Media).WithMany(p => p.KioskSettingsHomeImages)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountMedia>(entity =>
        {
            entity.HasKey(e => new { e.AccountId, e.MediaId });

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.AccountMedia)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AccountMedia_Account");

            entity.HasOne(d => d.Media).WithMany(p => p.AccountMedia)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AccountMedia_Media");
        });

        modelBuilder.Entity<Attribute>(entity =>
        {
            entity.HasIndex(e => new { e.SiteId, e.Name }, "UX_Attribute_SiteId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.AttributeCreationUsers).HasConstraintName("FK_Attribute_CreationUser");

            entity.HasOne(d => d.Site).WithMany(p => p.Attributes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attribute_Site");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.AttributeUpdateUsers).HasConstraintName("FK_Attribute_UpdateUser");
        });

        modelBuilder.Entity<AttributeValue>(entity =>
        {
            entity.HasOne(d => d.Attribute).WithMany(p => p.AttributeValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttributeValue_Attribute");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Brand_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Brands).HasConstraintName("FK_Brand_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.BrandCreationUsers).HasConstraintName("FK_Brand_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.BrandUpdateUsers).HasConstraintName("FK_Brand_UpdateUser");
        });

        modelBuilder.Entity<BusinessType>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.BusinessTypeCreationUsers).HasConstraintName("FK_BusinessType_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.BusinessTypeUpdateUsers).HasConstraintName("FK_BusinessType_UpdateUser");
        });

        modelBuilder.Entity<BusinessTypeCategory>(entity =>
        {
            entity.HasOne(d => d.BusinessType).WithMany(p => p.BusinessTypeCategories)
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

            entity.HasOne(d => d.Account).WithMany(p => p.Categories).HasConstraintName("FK_Category_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.CategoryCreationUsers).HasConstraintName("FK_Category_CreationUser");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory).HasConstraintName("FK_Category_Parent");

            entity.HasOne(d => d.SourceGlobalCategory).WithMany(p => p.Categories).HasConstraintName("FK_Category_SourceGlobalCategory");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.CategoryUpdateUsers).HasConstraintName("FK_Category_UpdateUser");

            entity.HasMany(d => d.Sites).WithMany(p => p.Categories)
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
                        j.ToTable("CategorySite");
                        j.HasIndex(new[] { "SiteId" }, "IX_CategorySite_SiteId");
                    });
        });

        modelBuilder.Entity<GlobalCategory>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.GlobalCategoryCreationUsers).HasConstraintName("FK_GlobalCategory_CreationUser");

            entity.HasOne(d => d.ParentGlobalCategory).WithMany(p => p.InverseParentGlobalCategory).HasConstraintName("FK_GlobalCategory_Parent");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.GlobalCategoryUpdateUsers).HasConstraintName("FK_GlobalCategory_UpdateUser");

            entity.HasMany(d => d.BusinessTypes).WithMany(p => p.GlobalCategories)
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
                        j.ToTable("GlobalCategoryBusinessType");
                    });
        });

        modelBuilder.Entity<Medium>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.BusinessType).WithMany(p => p.Media).HasConstraintName("FK_Media_BusinessType");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.MediumCreationUsers).HasConstraintName("FK_Media_CreationUser");

            entity.HasOne(d => d.Type).WithMany(p => p.Media).HasConstraintName("FK_Media_Type");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.MediumUpdateUsers).HasConstraintName("FK_Media_UpdateUser");

            entity.HasMany(d => d.Categories).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaCategory_Category"),
                    l => l.HasOne<Medium>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaCategory_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "CategoryId");
                        j.ToTable("MediaCategory");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaTag_Tag"),
                    l => l.HasOne<Medium>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaTag_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "TagId");
                        j.ToTable("MediaTag");
                        j.HasIndex(new[] { "TagId" }, "IX_MediaTag_TagId");
                    });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Sku }, "UX_Product_AccountId_Sku_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [Sku] IS NOT NULL AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Brand).WithMany(p => p.Products).HasConstraintName("FK_Product_Brand");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.ProductCreationUsers).HasConstraintName("FK_Product_CreationUser");

            entity.HasOne(d => d.SetupType).WithMany(p => p.Products).HasConstraintName("FK_Product_SetupType");

            entity.HasOne(d => d.ShippingClass).WithMany(p => p.Products).HasConstraintName("FK_Product_ShippingClass");

            entity.HasOne(d => d.Status).WithMany(p => p.Products).HasConstraintName("FK_Product_Status");

            entity.HasOne(d => d.StockManagementType).WithMany(p => p.Products).HasConstraintName("FK_Product_StockManagementType");

            entity.HasOne(d => d.StockStatus).WithMany(p => p.Products).HasConstraintName("FK_Product_StockStatus");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Products).HasConstraintName("FK_Product_Supplier");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.ProductUpdateUsers).HasConstraintName("FK_Product_UpdateUser");

            entity.HasOne(d => d.Visibility).WithMany(p => p.Products).HasConstraintName("FK_Product_Visibility");

            entity.HasOne(d => d.WeightConfig).WithMany(p => p.Products).HasConstraintName("FK_Product_WeightConfig");

            entity.HasMany(d => d.Sites).WithMany(p => p.Products)
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
                        j.ToTable("ProductSite");
                        j.HasIndex(new[] { "SiteId" }, "IX_ProductSite_SiteId");
                    });

            entity.HasMany(d => d.RelatedProducts).WithMany()
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
                        j.ToTable("ProductRelated");
                    });

            entity.HasMany(d => d.ComplementaryProducts).WithMany()
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
                        j.ToTable("ProductComplementary");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Products)
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
                        j.ToTable("ProductTag");
                        j.HasIndex(new[] { "TagId" }, "IX_ProductTag_TagId");
                    });
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasOne(d => d.Category).WithMany(p => p.ProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategory_Category");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategory_Product");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.Url }).IsUnique().HasDatabaseName("IX_ProductImage_ProductId_Url");
            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImage_Product");
            entity.HasOne(d => d.Media).WithMany(m => m.ProductImages)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ProductImage_Media");
        });

        modelBuilder.Entity<ProductOption>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.ProductOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductOption_Product");
        });

        modelBuilder.Entity<ProductOptionValue>(entity =>
        {
            entity.HasOne(d => d.ProductOption).WithMany(p => p.ProductOptionValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductOptionValue_ProductOption");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVariant_Product");
        });

        modelBuilder.Entity<ProductVariantOptionValue>(entity =>
        {
            entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductVariantOptionValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductVariantOptionValue_ProductVariant");
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Currency).HasDefaultValue("ILS");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.Sites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Site_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.SiteCreationUsers).HasConstraintName("FK_Site_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.SiteUpdateUsers).HasConstraintName("FK_Site_UpdateUser");

            entity.HasMany(d => d.BusinessTypes).WithMany(p => p.Sites)
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
                        j.ToTable("SiteBusinessType");
                    });

            entity.HasMany(d => d.Users).WithMany(p => p.Sites)
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
                        j.ToTable("SiteUser");
                    });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => new { e.SiteId, e.IsDeleted }).HasFilter("([IsDeleted]=(0))");
            entity.HasIndex(e => e.OrderNumber).HasFilter("([IsDeleted]=(0))");
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.Account).WithMany(p => p.Orders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Account");
            entity.HasOne(d => d.Site).WithMany(p => p.Orders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Site");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_OrderItem_Order");
        });

        modelBuilder.Entity<SiteOrderReceptionClosed>(entity =>
        {
            entity.HasIndex(e => new { e.SiteId, e.ClosedDate, e.Type }).IsUnique();
            entity.HasOne(d => d.Site).WithMany(p => p.SiteOrderReceptionClosed)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SiteOrderReceptionClosed_Site");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Supplier_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Suppliers).HasConstraintName("FK_Supplier_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.SupplierCreationUsers).HasConstraintName("FK_Supplier_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.SupplierUpdateUsers).HasConstraintName("FK_Supplier_UpdateUser");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => new { e.AccountId, e.Name }, "UX_Tag_AccountId_Name_NotDeleted")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [AccountId] IS NOT NULL)");

            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Account).WithMany(p => p.Tags).HasConstraintName("FK_Tag_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TagCreationUsers).HasConstraintName("FK_Tag_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TagUpdateUsers).HasConstraintName("FK_Tag_UpdateUser");
        });

        modelBuilder.Entity<TemplateAttribute>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TemplateAttributeCreationUsers).HasConstraintName("FK_TemplateAttribute_CreationUser");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TemplateAttributeUpdateUsers).HasConstraintName("FK_TemplateAttribute_UpdateUser");

            entity.HasMany(d => d.Sites).WithMany(p => p.TemplateAttributes)
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
                        j.ToTable("TemplateAttributeSite");
                    });
        });

        modelBuilder.Entity<TemplateAttributeValue>(entity =>
        {
            entity.HasOne(d => d.TemplateAttribute).WithMany(p => p.TemplateAttributeValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateAttributeValue_TemplateAttribute");
        });

        modelBuilder.Entity<TemplateProduct>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Brand).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_Brand");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.TemplateProductCreationUsers).HasConstraintName("FK_TemplateProduct_CreationUser");

            entity.HasOne(d => d.SetupType).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_SetupType");

            entity.HasOne(d => d.ShippingClass).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_ShippingClass");

            entity.HasOne(d => d.Status).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_Status");

            entity.HasOne(d => d.StockManagementType).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_StockManagementType");

            entity.HasOne(d => d.StockStatus).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_StockStatus");

            entity.HasOne(d => d.Supplier).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_Supplier");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.TemplateProductUpdateUsers).HasConstraintName("FK_TemplateProduct_UpdateUser");

            entity.HasOne(d => d.Visibility).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_Visibility");

            entity.HasOne(d => d.WeightConfig).WithMany(p => p.TemplateProducts).HasConstraintName("FK_TemplateProduct_WeightConfig");

            entity.HasMany(d => d.Sites).WithMany(p => p.TemplateProducts)
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
                        j.ToTable("TemplateProductSite");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.TemplateProducts)
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
                        j.ToTable("TemplateProductTag");
                    });

            entity.HasMany(d => d.RelatedProducts).WithMany()
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
                        j.ToTable("TemplateProductRelated");
                    });

            entity.HasMany(d => d.ComplementaryProducts).WithMany()
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
                        j.ToTable("TemplateProductComplementary");
                    });
        });

        modelBuilder.Entity<TemplateProductCategory>(entity =>
        {
            entity.HasOne(d => d.GlobalCategory).WithMany(p => p.TemplateProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPC_GlobalCategory");

            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPC_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductImage>(entity =>
        {
            entity.HasIndex(e => new { e.TemplateProductId, e.Url }).IsUnique().HasDatabaseName("IX_TemplateProductImage_TemplateProductId_Url");
            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductImages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductImage_TemplateProduct");
            entity.HasOne(d => d.Media).WithMany(m => m.TemplateProductImages)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TemplateProductImage_Media");
        });

        modelBuilder.Entity<TemplateProductOption>(entity =>
        {
            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductOption_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductOptionValue>(entity =>
        {
            entity.HasOne(d => d.TemplateProductOption).WithMany(p => p.TemplateProductOptionValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductOptionValue_TemplateProductOption");
        });

        modelBuilder.Entity<TemplateProductVariant>(entity =>
        {
            entity.HasOne(d => d.TemplateProduct).WithMany(p => p.TemplateProductVariants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TemplateProductVariant_TemplateProduct");
        });

        modelBuilder.Entity<TemplateProductVariantOptionValue>(entity =>
        {
            entity.HasOne(d => d.TemplateProductVariant).WithMany(p => p.TemplateProductVariantOptionValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TPVOV_TemplateProductVariant");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.CreationTime).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasComputedColumnSql("(([FirstName]+N' ')+[LastName])", true);
            entity.Property(e => e.GuidId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Account).WithMany(p => p.Users).HasConstraintName("FK_User_Account");

            entity.HasOne(d => d.CreationUser).WithMany(p => p.InverseCreationUser).HasConstraintName("FK_User_CreationUser");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Role");

            entity.HasOne(d => d.Status).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Status");

            entity.HasOne(d => d.UpdateUser).WithMany(p => p.InverseUpdateUser).HasConstraintName("FK_User_UpdateUser");
        });

        modelBuilder.Entity<WeightConfig>(entity =>
        {
            entity.HasOne(d => d.Unit).WithMany(p => p.WeightConfigs).HasConstraintName("FK_WeightConfig_Unit");

            entity.HasOne(d => d.UnitWeightMode).WithMany(p => p.WeightConfigs).HasConstraintName("FK_WeightConfig_UnitWeightMode");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
