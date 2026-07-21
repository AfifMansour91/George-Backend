-- ============================================================================
-- MultiSite Phase 2 — RUN-ALL migration bundle.
-- Runs every MultiSite migration in the REQUIRED order, in one go. Fully idempotent:
-- safe to re-run. ALWAYS use this (or run the files in this exact order) — never a
-- single migration alone, because the backend EF model spans all of them and the
-- self-repair (drop a malformed table missing [Id]) relies on later files re-adding
-- their columns. Run in SSMS against the George DB.
-- ============================================================================


-- ===================== AddMultiSiteProductOverrides.sql =====================

-- Migration: MultiSite Phase 2 — per-site product override layer.
-- Adds: ProductSiteOverride + ProductSiteVariantStock tables, Product.ManagementMode/OwnerSiteId,
--       Account.ManagementMode. Idempotent; run in SSMS or sqlcmd against your George DB.
-- See MultiSite/חוזה-בקאנד-מולטיסייט-Phase2-Override.md.

-- 1. Product: management mode columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = N'ManagementMode')
    ALTER TABLE [dbo].[Product] ADD [ManagementMode] nvarchar(20) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = N'OwnerSiteId')
    ALTER TABLE [dbo].[Product] ADD [OwnerSiteId] int NULL;
GO

-- 2. Account: ongoing management mode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = N'ManagementMode')
    ALTER TABLE [dbo].[Account] ADD [ManagementMode] nvarchar(20) NULL;
GO

-- 3. ProductSiteOverride table
-- Self-repair: an earlier partial run may have left this table WITHOUT its [Id] PK (the self-heal below cannot
-- add an IDENTITY PK). Such a table is unusable (EF selects [Id]); drop the malformed leftover so it is recreated
-- correctly. Only triggers when [Id] is missing — a healthy table is untouched. (QA override data is disposable.)
IF OBJECT_ID(N'[dbo].[ProductSiteOverride]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteOverride];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteOverride]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteOverride] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime2(0) NOT NULL,
        [UpdatedDate] datetime2(0) NULL,
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [AccountId] int NULL,
        [IsExcluded] bit NOT NULL DEFAULT 0,
        [Price] decimal(18, 2) NULL,
        [SalePrice] decimal(18, 2) NULL,
        [SalePriceStartDate] datetime2(0) NULL,
        [SalePriceEndDate] datetime2(0) NULL,
        [Availability] bit NULL,
        [StockManagementTypeId] int NULL,
        [StockStatusId] int NULL,
        [StockQuantity] decimal(18, 4) NULL,
        [VariationStockByQuantity] bit NULL,
        [LowStockThreshold] decimal(18, 4) NULL,
        CONSTRAINT [PK_ProductSiteOverride] PRIMARY KEY ([Id])
    );
END
GO

-- 3a. Self-heal columns (in case the table pre-existed from a partial run without all columns)
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsDeleted') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'CreationTime') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [CreationTime] datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'UpdatedDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [UpdatedDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'AccountId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [AccountId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsExcluded') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsExcluded] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Price') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Price] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePrice') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePrice] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePriceStartDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePriceStartDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePriceEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePriceEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Availability') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Availability] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockManagementTypeId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockManagementTypeId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockStatusId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockStatusId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockQuantity] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'VariationStockByQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [VariationStockByQuantity] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LowStockThreshold') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LowStockThreshold] decimal(18, 4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteOverride_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE UNIQUE INDEX [UX_ProductSiteOverride_Product_Site] ON [dbo].[ProductSiteOverride] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteOverride_SiteId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE INDEX [IX_ProductSiteOverride_SiteId] ON [dbo].[ProductSiteOverride] ([SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteOverride_AccountId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE INDEX [IX_ProductSiteOverride_AccountId] ON [dbo].[ProductSiteOverride] ([AccountId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteOverride_Product')
    ALTER TABLE [dbo].[ProductSiteOverride] ADD CONSTRAINT [FK_ProductSiteOverride_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteOverride_Site')
    ALTER TABLE [dbo].[ProductSiteOverride] ADD CONSTRAINT [FK_ProductSiteOverride_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- 4. ProductSiteVariantStock table
-- Self-repair: same as above — drop a malformed leftover missing its [Id] PK so it is recreated correctly.
-- (Re-run AddMultiSiteVariantPriceExclusion.sql afterwards to re-add the per-site variant Price/SalePrice/IsExcluded columns.)
IF OBJECT_ID(N'[dbo].[ProductSiteVariantStock]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteVariantStock];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteVariantStock]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteVariantStock] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime2(0) NOT NULL,
        [UpdatedDate] datetime2(0) NULL,
        [ProductVariantId] int NOT NULL,
        [SiteId] int NOT NULL,
        [ProductId] int NULL,
        [StockQuantity] decimal(18, 4) NULL,
        [StockStatusId] int NULL,
        CONSTRAINT [PK_ProductSiteVariantStock] PRIMARY KEY ([Id])
    );
END
GO

-- 4a. Self-heal columns (in case the table pre-existed from a partial run without all columns)
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'IsDeleted') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'CreationTime') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [CreationTime] datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'UpdatedDate') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [UpdatedDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'ProductVariantId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [ProductVariantId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [ProductId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'StockQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [StockQuantity] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'StockStatusId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [StockStatusId] int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteVariantStock_Variant_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE UNIQUE INDEX [UX_ProductSiteVariantStock_Variant_Site] ON [dbo].[ProductSiteVariantStock] ([ProductVariantId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantStock_SiteId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE INDEX [IX_ProductSiteVariantStock_SiteId] ON [dbo].[ProductSiteVariantStock] ([SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantStock_ProductId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE INDEX [IX_ProductSiteVariantStock_ProductId] ON [dbo].[ProductSiteVariantStock] ([ProductId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantStock_ProductVariant')
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD CONSTRAINT [FK_ProductSiteVariantStock_ProductVariant]
    FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantStock_Site')
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD CONSTRAINT [FK_ProductSiteVariantStock_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO


-- ===================== AddMultiSiteOverrideScalarFields.sql =====================

-- Migration: MultiSite Phase 2 — per-site scalar field overrides on ProductSiteOverride.
-- Adds Name, ShortDescription, LongDescription, Weight, WeightUnit, Sku, SeoTitle, SeoDescription
-- so name/description/weight/sku/seo can be overridden per branch (full per-site editing of scalars).
-- Idempotent. Run after AddMultiSiteProductOverrides.sql.

IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Name') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Name] nvarchar(300) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ShortDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ShortDescription] nvarchar(2000) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LongDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LongDescription] nvarchar(max) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Weight') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Weight] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'WeightUnit') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [WeightUnit] nvarchar(5) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Sku') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Sku] nvarchar(100) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SeoTitle') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SeoTitle] nvarchar(300) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SeoDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SeoDescription] nvarchar(2000) NULL;
GO


-- ===================== AddMultiSiteOverrideMerchandisingFields.sql =====================

-- Migration: MultiSite Phase 2 — per-site merchandising overrides on ProductSiteOverride.
-- Adds CostPrice, IsKosher, StatusId, VisibilityId, Slug, ShippingClassId, SupplierId and the storefront
-- Label* fields so every product field a branch (selected-site) edit touches follows the same model as
-- Price: a canonical all-sites value + an optional per-site override (null = inherit canonical).
-- Idempotent. Run after AddMultiSiteOverrideScalarFields.sql.

IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'CostPrice') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [CostPrice] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsKosher') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsKosher] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StatusId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StatusId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'VisibilityId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [VisibilityId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Slug') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Slug] nvarchar(200) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ShippingClassId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ShippingClassId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SupplierId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SupplierId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelFrozen') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelFrozen] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelGlutenFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelGlutenFree] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNotKosher') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNotKosher] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelKosherForPassover') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelKosherForPassover] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelKosherForPassoverEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelKosherForPassoverEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNew') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNew] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNewEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNewEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelBestseller') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelBestseller] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelLowAvailability') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelLowAvailability] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelReadyToCook') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelReadyToCook] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNatural') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNatural] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelSugarFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelSugarFree] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelLactoseFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelLactoseFree] bit NULL;
GO


-- ===================== AddProductSiteCategory.sql =====================

-- Migration: MultiSite Phase 2 — per-site category assignment (ProductSiteCategory).
-- A product can be assigned to different categories per branch. When rows exist for a (product, site),
-- they replace the canonical ProductCategory for that site (effective view). Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteCategory]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteCategory];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteCategory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteCategory] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [CategoryId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteCategory] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'CategoryId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [CategoryId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteCategory_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteCategory]'))
    CREATE INDEX [IX_ProductSiteCategory_Product_Site] ON [dbo].[ProductSiteCategory] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteCategory_Product_Site_Category' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteCategory]'))
    CREATE UNIQUE INDEX [UX_ProductSiteCategory_Product_Site_Category] ON [dbo].[ProductSiteCategory] ([ProductId], [SiteId], [CategoryId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Product')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Site')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Category')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Category]
    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]);
GO


-- ===================== AddProductSiteImage.sql =====================

-- Migration: MultiSite Phase 2 — per-site product images (ProductSiteImage).
-- A product can have different images per branch. When rows exist for a (product, site), they replace
-- the canonical ProductImage list for that site (effective view). Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteImage]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteImage]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteImage];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteImage]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteImage] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ProductSiteImage] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'Url') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [Url] nvarchar(1000) NOT NULL DEFAULT '';
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'SortOrder') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [SortOrder] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteImage_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteImage]'))
    CREATE INDEX [IX_ProductSiteImage_Product_Site] ON [dbo].[ProductSiteImage] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteImage_Product')
    ALTER TABLE [dbo].[ProductSiteImage] ADD CONSTRAINT [FK_ProductSiteImage_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteImage_Site')
    ALTER TABLE [dbo].[ProductSiteImage] ADD CONSTRAINT [FK_ProductSiteImage_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO


-- ===================== AddProductSiteWooId.sql =====================

-- Migration: MultiSite Phase 2 — per-site WooCommerce product id (ProductSiteWooId).
-- The same product has a DIFFERENT Woo product id in each site's store; the single Product.WooCommerceId
-- column can only track one, so syncing a multi-site product to the 2nd store used the wrong id and only
-- one site actually updated. This table maps (ProductId, SiteId) -> WooCommerceProductId. Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteWooId];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [WooCommerceProductId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'WooCommerceProductId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [WooCommerceProductId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteWooId_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteWooId]'))
    CREATE UNIQUE INDEX [UX_ProductSiteWooId_Product_Site] ON [dbo].[ProductSiteWooId] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteWooId_Product')
    ALTER TABLE [dbo].[ProductSiteWooId] ADD CONSTRAINT [FK_ProductSiteWooId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteWooId_Site')
    ALTER TABLE [dbo].[ProductSiteWooId] ADD CONSTRAINT [FK_ProductSiteWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for products on exactly ONE site (where Product.WooCommerceId
-- is unambiguously that site's id). Multi-site products are intentionally NOT seeded: their per-site ids are
-- resolved fresh via the sync's per-site SKU lookup, avoiding seeding a wrong id for the other store(s).
INSERT INTO [dbo].[ProductSiteWooId] ([ProductId], [SiteId], [WooCommerceProductId])
SELECT p.[Id], ps.[SiteId], p.[WooCommerceId]
FROM [dbo].[Product] p
JOIN [dbo].[ProductSite] ps ON ps.[ProductId] = p.[Id]
WHERE p.[WooCommerceId] IS NOT NULL
  AND (SELECT COUNT(*) FROM [dbo].[ProductSite] ps2 WHERE ps2.[ProductId] = p.[Id]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[ProductSiteWooId] x WHERE x.[ProductId] = p.[Id] AND x.[SiteId] = ps.[SiteId]);
GO


-- ===================== AddCategorySiteWooId.sql =====================

-- Migration: MultiSite Phase 2 — per-site WooCommerce category id (CategorySiteWooId).
-- A category shared across sites (network mode) has a DIFFERENT Woo category id in each store; the single
-- Category.WooCommerceId column can only track one, so syncing a shared category to the 2nd store reused the
-- 1st store's id and overwrote/corrupted whatever category sat at that id ("categories mess"). This table maps
-- (CategoryId, SiteId) -> WooCommerceCategoryId. Consulted only for network-managed accounts. Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[CategorySiteWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[CategorySiteWooId];
GO

IF OBJECT_ID(N'[dbo].[CategorySiteWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CategorySiteWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [CategoryId] int NOT NULL,
        [SiteId] int NOT NULL,
        [WooCommerceCategoryId] int NOT NULL,
        CONSTRAINT [PK_CategorySiteWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'CategoryId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [CategoryId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'WooCommerceCategoryId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [WooCommerceCategoryId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CategorySiteWooId_Category_Site' AND object_id = OBJECT_ID(N'[dbo].[CategorySiteWooId]'))
    CREATE UNIQUE INDEX [UX_CategorySiteWooId_Category_Site] ON [dbo].[CategorySiteWooId] ([CategoryId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CategorySiteWooId_Category')
    ALTER TABLE [dbo].[CategorySiteWooId] ADD CONSTRAINT [FK_CategorySiteWooId_Category]
    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CategorySiteWooId_Site')
    ALTER TABLE [dbo].[CategorySiteWooId] ADD CONSTRAINT [FK_CategorySiteWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for categories on exactly ONE site (where Category.WooCommerceId
-- is unambiguously that site's id). Categories shared across multiple sites are intentionally NOT seeded: their
-- per-site ids are resolved fresh (find-or-create by name) on next sync, avoiding seeding a wrong store's id.
INSERT INTO [dbo].[CategorySiteWooId] ([CategoryId], [SiteId], [WooCommerceCategoryId])
SELECT c.[Id], cs.[SiteId], c.[WooCommerceId]
FROM [dbo].[Category] c
JOIN [dbo].[CategorySite] cs ON cs.[CategoryId] = c.[Id]
WHERE c.[WooCommerceId] IS NOT NULL
  AND (SELECT COUNT(*) FROM [dbo].[CategorySite] cs2 WHERE cs2.[CategoryId] = c.[Id]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySiteWooId] x WHERE x.[CategoryId] = c.[Id] AND x.[SiteId] = cs.[SiteId]);
GO


-- ===================== BackfillOrphanCategorySites.sql =====================

-- Migration: backfill CategorySite links for "orphan" categories.
-- Categories imported / created in "all sites" mode were linked to NO site (empty site list), so they were
-- invisible per-branch in the UI and skipped by the per-site WooCommerce category sync. The code now expands an
-- empty site list to all of the account's sites; this backfills the rows already created broken. Idempotent.
--
-- For every non-deleted category that has an AccountId but ZERO CategorySite rows, link it to every non-deleted
-- site of that account.
INSERT INTO [dbo].[CategorySite] ([CategoryId], [SiteId])
SELECT c.[Id], s.[Id]
FROM [dbo].[Category] c
JOIN [dbo].[Site] s
  ON s.[AccountId] = c.[AccountId]
 AND s.[IsDeleted] = 0
WHERE c.[IsDeleted] = 0
  AND c.[AccountId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySite] cs WHERE cs.[CategoryId] = c.[Id])
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySite] x WHERE x.[CategoryId] = c.[Id] AND x.[SiteId] = s.[Id]);
GO


-- ===================== AddMultiSiteVariantPriceExclusion.sql =====================

-- Migration: MultiSite Phase 2 — per-site variant price + exclusion.
-- Extends ProductSiteVariantStock (already per-(variant,site) stock) with per-site variant Price/SalePrice and
-- an IsExcluded flag, so a network product's variations can be priced per branch and a variation can be
-- "removed" in one branch (hidden there) without deleting it from the canonical product. Idempotent.

IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'Price') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [Price] decimal(18, 2) NULL;
GO
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'SalePrice') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [SalePrice] decimal(18, 2) NULL;
GO
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'IsExcluded') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [IsExcluded] bit NOT NULL DEFAULT 0;
GO


-- ===================== AddProductSiteVariantWooId.sql =====================

-- Migration: MultiSite Phase 2 — per-site WooCommerce VARIATION id (ProductSiteVariantWooId).
-- The same variant has a DIFFERENT Woo variation id in each store; the single ProductVariant.WooCommerceVariationId
-- column can only track one, so syncing a shared VARIABLE product to the 2nd store reused the 1st store's variation
-- ids → the PUT 404'd, the variation was recreated, then the orphan-cleanup deleted the recreation (it tracked the
-- OLD id) — leaving the 2nd store with ZERO variations, which WooCommerce reports as out of stock. This table maps
-- (ProductVariantId, SiteId) -> WooCommerceVariationId. Consulted only for network-managed accounts. Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteVariantWooId];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteVariantWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductVariantId] int NOT NULL,
        [SiteId] int NOT NULL,
        [ProductId] int NOT NULL,
        [WooCommerceVariationId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteVariantWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'ProductVariantId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [ProductVariantId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'WooCommerceVariationId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [WooCommerceVariationId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteVariantWooId_Variant_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]'))
    CREATE UNIQUE INDEX [UX_ProductSiteVariantWooId_Variant_Site] ON [dbo].[ProductSiteVariantWooId] ([ProductVariantId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantWooId_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]'))
    CREATE INDEX [IX_ProductSiteVariantWooId_Product_Site] ON [dbo].[ProductSiteVariantWooId] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantWooId_ProductVariant')
    ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD CONSTRAINT [FK_ProductSiteVariantWooId_ProductVariant]
    FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantWooId_Site')
    ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD CONSTRAINT [FK_ProductSiteVariantWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for variants whose product is on exactly ONE site (unambiguous).
-- Variants of products shared across multiple sites are intentionally NOT seeded: their per-site variation ids are
-- resolved fresh (match by attribute signature against each store's live variations) on next sync.
INSERT INTO [dbo].[ProductSiteVariantWooId] ([ProductVariantId], [SiteId], [ProductId], [WooCommerceVariationId])
SELECT v.[Id], ps.[SiteId], v.[ProductId], v.[WooCommerceVariationId]
FROM [dbo].[ProductVariant] v
JOIN [dbo].[ProductSite] ps ON ps.[ProductId] = v.[ProductId]
WHERE v.[WooCommerceVariationId] IS NOT NULL
  AND v.[IsDeleted] = 0
  AND (SELECT COUNT(*) FROM [dbo].[ProductSite] ps2 WHERE ps2.[ProductId] = v.[ProductId]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[ProductSiteVariantWooId] x WHERE x.[ProductVariantId] = v.[Id] AND x.[SiteId] = ps.[SiteId]);
GO

