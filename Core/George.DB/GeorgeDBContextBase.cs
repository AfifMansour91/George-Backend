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

    public virtual DbSet<AccountBusinessType> AccountBusinessTypes { get; set; }

    public virtual DbSet<AccountCategory> AccountCategories { get; set; }

    public virtual DbSet<AccountCategorySite> AccountCategorySites { get; set; }

    public virtual DbSet<AccountEcomCredential> AccountEcomCredentials { get; set; }

    public virtual DbSet<AccountProduct> AccountProducts { get; set; }

    public virtual DbSet<AccountProductAttribute> AccountProductAttributes { get; set; }

    public virtual DbSet<AccountProductAttributeValue> AccountProductAttributeValues { get; set; }

    public virtual DbSet<AccountProductCategory> AccountProductCategories { get; set; }

    public virtual DbSet<AccountProductImportStaging> AccountProductImportStagings { get; set; }

    public virtual DbSet<AccountProductMedium> AccountProductMedia { get; set; }

    public virtual DbSet<AccountProductSite> AccountProductSites { get; set; }

    public virtual DbSet<AccountProductTag> AccountProductTags { get; set; }

    public virtual DbSet<AccountProductVariant> AccountProductVariants { get; set; }

    public virtual DbSet<AccountProductVariantOption> AccountProductVariantOptions { get; set; }

    public virtual DbSet<AccountUser> AccountUsers { get; set; }

    public virtual DbSet<Attribute> Attributes { get; set; }

    public virtual DbSet<AttributeOption> AttributeOptions { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<BusinessType> BusinessTypes { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CategoryBusinessType> CategoryBusinessTypes { get; set; }

    public virtual DbSet<CategoryHierarchy> CategoryHierarchies { get; set; }

    public virtual DbSet<EcomCategoryMap> EcomCategoryMaps { get; set; }

    public virtual DbSet<EcomPlatform> EcomPlatforms { get; set; }

    public virtual DbSet<EcomProductMap> EcomProductMaps { get; set; }

    public virtual DbSet<EcomVariantMap> EcomVariantMaps { get; set; }

    public virtual DbSet<KosherStatus> KosherStatuses { get; set; }

    public virtual DbSet<Medium> Media { get; set; }

    public virtual DbSet<ProductEditLog> ProductEditLogs { get; set; }

    public virtual DbSet<ProductTemplate> ProductTemplates { get; set; }

    public virtual DbSet<ProductTemplateAttribute> ProductTemplateAttributes { get; set; }

    public virtual DbSet<ProductTemplateAttributeOption> ProductTemplateAttributeOptions { get; set; }

    public virtual DbSet<ProductTemplateAttributeSite> ProductTemplateAttributeSites { get; set; }

    public virtual DbSet<ProductTemplateBusinessType> ProductTemplateBusinessTypes { get; set; }

    public virtual DbSet<ProductTemplateCategory> ProductTemplateCategories { get; set; }

    public virtual DbSet<ProductTemplateMedium> ProductTemplateMedia { get; set; }

    public virtual DbSet<ProductTemplateSelectableWeight> ProductTemplateSelectableWeights { get; set; }

    public virtual DbSet<ProductType> ProductTypes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Site> Sites { get; set; }

    public virtual DbSet<SiteBusinessType> SiteBusinessTypes { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SyncJob> SyncJobs { get; set; }

    public virtual DbSet<SyncJobLog> SyncJobLogs { get; set; }

    public virtual DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserStatus> UserStatuses { get; set; }

    public virtual DbSet<VwAccountCategoryTree> VwAccountCategoryTrees { get; set; }

    public virtual DbSet<VwAccountProductList> VwAccountProductLists { get; set; }

    public virtual DbSet<VwAccountProductStatusSummary> VwAccountProductStatusSummaries { get; set; }

    public virtual DbSet<WeightPricingModel> WeightPricingModels { get; set; }

    public virtual DbSet<WizardSession> WizardSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(e => e.Base44Id, "UX_Account_Base44Id")
                .IsUnique()
                .HasFilter("([Base44Id] IS NOT NULL)");

            entity.Property(e => e.AllowWeighted).HasDefaultValue(true);
            entity.Property(e => e.ContentOwner).HasDefaultValue("Company");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsKosherShop).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValue("Active");
            entity.Property(e => e.WizardStatus).HasDefaultValue("Not Started");
        });

        modelBuilder.Entity<AccountBusinessType>(entity =>
        {
            entity.Property(e => e.IsSelected).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountBusinessType_Account");

            entity.HasOne(d => d.BusinessType).WithMany(p => p.AccountBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountBusinessType_BusinessType");
        });

        modelBuilder.Entity<AccountCategory>(entity =>
        {
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountCategory_Account");

            entity.HasOne(d => d.Category).WithMany(p => p.AccountCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountCategory_Category");

            entity.HasOne(d => d.ParentAccountCategory).WithMany(p => p.InverseParentAccountCategory).HasConstraintName("FK_AccountCategory_Parent");
        });

        modelBuilder.Entity<AccountCategorySite>(entity =>
        {
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.AccountCategory).WithMany(p => p.AccountCategorySites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountCategorySite_AccountCategory");

            entity.HasOne(d => d.Site).WithMany(p => p.AccountCategorySites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountCategorySite_Site");
        });

        modelBuilder.Entity<AccountEcomCredential>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountEcomCredentials)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountEcomCredential_Account");

            entity.HasOne(d => d.EcomPlatform).WithMany(p => p.AccountEcomCredentials)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountEcomCredential_EcomPlatform");
        });

        modelBuilder.Entity<AccountProduct>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.EditingStatus).HasDefaultValue("NotEdited");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountProducts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProduct_Account");

            entity.HasOne(d => d.BaseUnit).WithMany(p => p.AccountProducts).HasConstraintName("FK_AccountProduct_Unit");

            entity.HasOne(d => d.Brand).WithMany(p => p.AccountProducts).HasConstraintName("FK_AccountProduct_Brand");

            entity.HasOne(d => d.KosherStatus).WithMany(p => p.AccountProducts).HasConstraintName("FK_AccountProduct_KosherStatus");

            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.AccountProducts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProduct_ProductTemplate");

            entity.HasOne(d => d.Supplier).WithMany(p => p.AccountProducts).HasConstraintName("FK_AccountProduct_Supplier");

            entity.HasOne(d => d.WeightPricingModel).WithMany(p => p.AccountProducts).HasConstraintName("FK_AccountProduct_WeightPricingModel");
        });

        modelBuilder.Entity<AccountProductAttribute>(entity =>
        {
            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductAttributes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductAttribute_AccountProduct");

            entity.HasOne(d => d.Attribute).WithMany(p => p.AccountProductAttributes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductAttribute_Attribute");
        });

        modelBuilder.Entity<AccountProductAttributeValue>(entity =>
        {
            entity.HasOne(d => d.AccountProductAttribute).WithMany(p => p.AccountProductAttributeValues)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductAttributeValue_AccountProductAttribute");

            entity.HasOne(d => d.AttributeOption).WithMany(p => p.AccountProductAttributeValues).HasConstraintName("FK_AccountProductAttributeValue_AttributeOption");
        });

        modelBuilder.Entity<AccountProductCategory>(entity =>
        {
            entity.HasOne(d => d.AccountCategory).WithMany(p => p.AccountProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductCategory_AccountCategory");

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductCategory_AccountProduct");
        });

        modelBuilder.Entity<AccountProductImportStaging>(entity =>
        {
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Account).WithMany(p => p.AccountProductImportStagings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductImportStaging_Account");

            entity.HasOne(d => d.MatchedProductTemplate).WithMany(p => p.AccountProductImportStagings).HasConstraintName("FK_AccountProductImportStaging_ProductTemplate");
        });

        modelBuilder.Entity<AccountProductMedium>(entity =>
        {
            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductMedia)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductMedia_AccountProduct");

            entity.HasOne(d => d.Media).WithMany(p => p.AccountProductMedia).HasConstraintName("FK_AccountProductMedia_Media");
        });

        modelBuilder.Entity<AccountProductSite>(entity =>
        {
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductSite_AccountProduct");

            entity.HasOne(d => d.Site).WithMany(p => p.AccountProductSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductSite_Site");
        });

        modelBuilder.Entity<AccountProductTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AccountP__3214EC07A53582F2");

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductTags)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductTag_AccountProduct");

            entity.HasOne(d => d.Tag).WithMany(p => p.AccountProductTags)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductTag_Tag");
        });

        modelBuilder.Entity<AccountProductVariant>(entity =>
        {
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.AccountProductVariants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductVariant_AccountProduct");
        });

        modelBuilder.Entity<AccountProductVariantOption>(entity =>
        {
            entity.HasOne(d => d.AccountProductVariant).WithMany(p => p.AccountProductVariantOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductVariantOption_AccountProductVariant");

            entity.HasOne(d => d.Attribute).WithMany(p => p.AccountProductVariantOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductVariantOption_Attribute");

            entity.HasOne(d => d.AttributeOption).WithMany(p => p.AccountProductVariantOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountProductVariantOption_AttributeOption");
        });

        modelBuilder.Entity<AccountUser>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Account).WithMany(p => p.AccountUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountUser_Account");

            entity.HasOne(d => d.Role).WithMany(p => p.AccountUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountUser_Role");

            entity.HasOne(d => d.Site).WithMany(p => p.AccountUsers).HasConstraintName("FK_AccountUser_Site");

            entity.HasOne(d => d.User).WithMany(p => p.AccountUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AccountUser_User");
        });

        modelBuilder.Entity<AttributeOption>(entity =>
        {
            entity.HasOne(d => d.Attribute).WithMany(p => p.AttributeOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttributeOption_Attribute");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs).HasConstraintName("FK_AuditLog_User");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<CategoryBusinessType>(entity =>
        {
            entity.HasOne(d => d.BusinessType).WithMany(p => p.CategoryBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoryBusinessType_BusinessType");

            entity.HasOne(d => d.Category).WithMany(p => p.CategoryBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoryBusinessType_Category");
        });

        modelBuilder.Entity<CategoryHierarchy>(entity =>
        {
            entity.HasOne(d => d.ChildCategory).WithMany(p => p.CategoryHierarchyChildCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoryHierarchy_Child");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.CategoryHierarchyParentCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CategoryHierarchy_Parent");
        });

        modelBuilder.Entity<EcomCategoryMap>(entity =>
        {
            entity.Property(e => e.SyncedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.AccountCategory).WithMany(p => p.EcomCategoryMaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EcomCategoryMap_AccountCategory");
        });

        modelBuilder.Entity<EcomProductMap>(entity =>
        {
            entity.Property(e => e.SyncedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.EcomProductMaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EcomProductMap_AccountProduct");
        });

        modelBuilder.Entity<EcomVariantMap>(entity =>
        {
            entity.Property(e => e.SyncedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.AccountProductVariant).WithMany(p => p.EcomVariantMaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EcomVariantMap_AccountProductVariant");
        });

        modelBuilder.Entity<Medium>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.BusinessType).WithMany(p => p.Media).HasConstraintName("FK_Media_BusinessType");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Media).HasConstraintName("FK_Media_CreatedByUser");

            entity.HasMany(d => d.Categories).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_MediaCategory_Category"),
                    l => l.HasOne<Medium>().WithMany()
                        .HasForeignKey("MediaId")
                        .HasConstraintName("FK_MediaCategory_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "CategoryId");
                        j.ToTable("MediaCategory");
                        j.HasIndex(new[] { "CategoryId" }, "IX_MediaCategory_CategoryId");
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
                        .HasConstraintName("FK_MediaTag_Media"),
                    j =>
                    {
                        j.HasKey("MediaId", "TagId");
                        j.ToTable("MediaTag");
                        j.HasIndex(new[] { "TagId" }, "IX_MediaTag_TagId");
                    });
        });

        modelBuilder.Entity<ProductEditLog>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.AccountProduct).WithMany(p => p.ProductEditLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductEditLog_AccountProduct");

            entity.HasOne(d => d.User).WithMany(p => p.ProductEditLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductEditLog_User");
        });

        modelBuilder.Entity<ProductTemplate>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsKosherDefault).HasDefaultValue(true);
            entity.Property(e => e.SetupType).HasDefaultValue("standard");

            entity.HasOne(d => d.BaseUnit).WithMany(p => p.ProductTemplates).HasConstraintName("FK_ProductTemplate_Unit");

            entity.HasOne(d => d.Brand).WithMany(p => p.ProductTemplates).HasConstraintName("FK_ProductTemplate_Brand");

            entity.HasOne(d => d.KosherStatus).WithMany(p => p.ProductTemplates).HasConstraintName("FK_ProductTemplate_KosherStatus");

            entity.HasOne(d => d.ProductType).WithMany(p => p.ProductTemplates)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplate_ProductType");

            entity.HasOne(d => d.Supplier).WithMany(p => p.ProductTemplates).HasConstraintName("FK_ProductTemplate_Supplier");

            entity.HasOne(d => d.WeightPricingModel).WithMany(p => p.ProductTemplates).HasConstraintName("FK_ProductTemplate_WeightPricingModel");

            entity.HasMany(d => d.Tags).WithMany(p => p.ProductTemplates)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductTemplateTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTemplateTag_Tag"),
                    l => l.HasOne<ProductTemplate>().WithMany()
                        .HasForeignKey("ProductTemplateId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ProductTemplateTag_ProductTemplate"),
                    j =>
                    {
                        j.HasKey("ProductTemplateId", "TagId");
                        j.ToTable("ProductTemplateTag");
                    });
        });

        modelBuilder.Entity<ProductTemplateAttribute>(entity =>
        {
            entity.HasOne(d => d.Attribute).WithMany(p => p.ProductTemplateAttributes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttribute_Attribute");

            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.ProductTemplateAttributes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttribute_ProductTemplate");
        });

        modelBuilder.Entity<ProductTemplateAttributeOption>(entity =>
        {
            entity.HasOne(d => d.AttributeOption).WithMany(p => p.ProductTemplateAttributeOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttributeOption_AttributeOption");

            entity.HasOne(d => d.ProductTemplateAttribute).WithMany(p => p.ProductTemplateAttributeOptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttributeOption_ProductTemplateAttribute");
        });

        modelBuilder.Entity<ProductTemplateAttributeSite>(entity =>
        {
            entity.HasOne(d => d.ProductTemplateAttribute).WithMany(p => p.ProductTemplateAttributeSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttributeSite_ProductTemplateAttribute");

            entity.HasOne(d => d.Site).WithMany(p => p.ProductTemplateAttributeSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateAttributeSite_Site");
        });

        modelBuilder.Entity<ProductTemplateBusinessType>(entity =>
        {
            entity.HasOne(d => d.BusinessType).WithMany(p => p.ProductTemplateBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateBusinessType_BusinessType");

            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.ProductTemplateBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateBusinessType_ProductTemplate");
        });

        modelBuilder.Entity<ProductTemplateCategory>(entity =>
        {
            entity.HasOne(d => d.Category).WithMany(p => p.ProductTemplateCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateCategory_Category");

            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.ProductTemplateCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateCategory_ProductTemplate");
        });

        modelBuilder.Entity<ProductTemplateMedium>(entity =>
        {
            entity.HasOne(d => d.Media).WithMany(p => p.ProductTemplateMedia).HasConstraintName("FK_ProductTemplateMedia_Media");

            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.ProductTemplateMedia)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateMedia_ProductTemplate");
        });

        modelBuilder.Entity<ProductTemplateSelectableWeight>(entity =>
        {
            entity.HasOne(d => d.ProductTemplate).WithMany(p => p.ProductTemplateSelectableWeights)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductTemplateSelectableWeight_ProductTemplate");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(e => e.AllowWeightedProducts).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("active");

            entity.HasOne(d => d.Account).WithMany(p => p.Sites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Site_Account");
        });

        modelBuilder.Entity<SiteBusinessType>(entity =>
        {
            entity.HasOne(d => d.BusinessType).WithMany(p => p.SiteBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SiteBusinessType_BusinessType");

            entity.HasOne(d => d.Site).WithMany(p => p.SiteBusinessTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SiteBusinessType_Site");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<SyncJob>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Account).WithMany(p => p.SyncJobs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SyncJob_Account");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.SyncJobs).HasConstraintName("FK_SyncJob_RequestedBy");
        });

        modelBuilder.Entity<SyncJobLog>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.SyncJob).WithMany(p => p.SyncJobLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SyncJobLog_SyncJob");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Tag__3214EC0770F07255");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_User_Email_Unique")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [Email] IS NOT NULL)");

            entity.Property(e => e.FullName).HasComputedColumnSql("(([Firstname]+' ')+[LastName])", true);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Role");

            entity.HasOne(d => d.Status).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_UserStatus");
        });

        modelBuilder.Entity<UserStatus>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<VwAccountCategoryTree>(entity =>
        {
            entity.ToView("vw_AccountCategoryTree");
        });

        modelBuilder.Entity<VwAccountProductList>(entity =>
        {
            entity.ToView("vw_AccountProductList");
        });

        modelBuilder.Entity<VwAccountProductStatusSummary>(entity =>
        {
            entity.ToView("vw_AccountProductStatusSummary");
        });

        modelBuilder.Entity<WizardSession>(entity =>
        {
            entity.Property(e => e.ContentOwner).HasDefaultValue("Company");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status).HasDefaultValue("InProgress");
            entity.Property(e => e.Step).HasDefaultValue(1);

            entity.HasOne(d => d.Account).WithMany(p => p.WizardSessions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WizardSession_Account");

            entity.HasOne(d => d.StartedByUser).WithMany(p => p.WizardSessions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WizardSession_StartedByUser");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
