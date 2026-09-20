-- =============================================================================
-- BUNDLES (מארזים) FEATURE - FULL PRODUCTION INSTALL (transaction-wrapped, idempotent).
-- Run once on a DB that has nothing bundle-related yet. Safe to re-run: every block is guarded.
-- Requires dbo.Account, dbo.Site, dbo.Product, dbo.ProductVariant, dbo.OrderItem, dbo.SetupType.
--
-- ATOMIC: the whole install runs inside one transaction. If any step fails, XACT_ABORT rolls it
-- back and the "IF XACT_STATE() = 0 SET NOEXEC ON" guards stop every later step.
--   * Recommended run: sqlcmd -b -i Bundles_Prod_Install.sql   (also fine in SSMS).
--   * If a run DID error: the changes were rolled back. The session may be left with NOEXEC ON -
--     run "SET NOEXEC OFF;" (or open a new window) before retrying.
--
-- Run order (do not reorder):
--   1. SetupType bundle row            2. Account.BundlesEnabled
--   3. Site bundle settings            4. ProductBundle* tables
--   5. OrderItem bundle columns        6. ProductStatus draft row
--   7. Product.PrintName               8. Site.ScanCompletesReadyOrder
--   9. Site.ProductPrintNameEnabled
--
-- Spec: shop-manager/docs/wooCommerceEngines/BUNDLES_SYNC_SPEC.md
-- Run on prod BEFORE deploying the backend that ships the bundles feature.
-- After this script the module is OFF for every account (Account.BundlesEnabled = 0). Switch it on per account from the
-- super-admin accounts screen. OPTIONAL, only if wanted: Account_EnableBundlesForAll.sql switches it ON for ALL accounts
-- and makes ON the DB default - do NOT run it when the rollout is account by account.
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;
-- Filtered index (IX_OrderItem_ParentOrderItemId) needs these ON; sqlcmd defaults QUOTED_IDENTIFIER OFF.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
GO


-- =============================================================================
-- SOURCE: SetupType_AddBundle.sql
-- =============================================================================

-- Bundles (מארזים): a bundle is a Product with SetupType 'bundle' (Id = 5).
-- Spec: shop-manager/docs/wooCommerceEngines/BUNDLES_SYNC_SPEC.md §1.
-- Idempotent: inserts the lookup row only when missing (by name or by id).

IF NOT EXISTS (SELECT 1 FROM dbo.SetupType WHERE Name = N'bundle')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SetupType WHERE Id = 5)
    BEGIN
        SET IDENTITY_INSERT dbo.SetupType ON;
        INSERT INTO dbo.SetupType (Id, Name, IsDeleted) VALUES (5, N'bundle', 0);
        SET IDENTITY_INSERT dbo.SetupType OFF;
    END
    ELSE
    BEGIN
        -- Id 5 is already taken by another row: let IDENTITY pick the next id (the code resolves by name).
        INSERT INTO dbo.SetupType (Name, IsDeleted) VALUES (N'bundle', 0);
    END
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Account_AddBundlesEnabled.sql
-- =============================================================================

-- Bundles (מארזים): account-level feature gate (pattern: Account.KioskEnabled).
-- Spec: BUNDLES_SYNC_SPEC.md §1 / §3.4. Idempotent.
-- Default OFF: adding the column leaves the module OFF for EVERY existing account (and for rows inserted outside the
-- API). It is switched on per account by a super admin (חשבונות → עריכה → "מארזים"); to switch it on for everyone
-- at once there is the separate, optional Account_EnableBundlesForAll.sql.

IF COL_LENGTH(N'dbo.Account', N'BundlesEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Account ADD
        BundlesEnabled BIT NOT NULL CONSTRAINT DF_Account_BundlesEnabled DEFAULT (0);
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Site_AddBundleSettings.sql
-- =============================================================================

-- Bundles (מארזים): per-site OC Bundles API key (X-OC-Bundles-Key, write-only in the API)
-- and the "free swap" permission for pickers/managers.
-- Spec: BUNDLES_SYNC_SPEC.md §1 / §3.4. Idempotent.

IF COL_LENGTH(N'dbo.Site', N'BundlesApiKey') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD BundlesApiKey NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'BundleAllowFreeSwap') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD BundleAllowFreeSwap BIT NULL;
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: ProductBundle_CreateTables.sql
-- =============================================================================

-- Bundles (מארזים): bundle definition tables. A bundle is a Product (SetupType 'bundle');
-- these tables hold its pricing/display config, its component slots and the allowed swaps per slot.
-- Spec: BUNDLES_SYNC_SPEC.md §2. Idempotent.

IF OBJECT_ID(N'dbo.ProductBundleConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBundleConfig (
        ProductId            INT            NOT NULL CONSTRAINT PK_ProductBundleConfig PRIMARY KEY,
        PricingMode          NVARCHAR(10)   NOT NULL CONSTRAINT DF_ProductBundleConfig_PricingMode DEFAULT (N'fixed'),          -- fixed | sum
        FixedPrice           DECIMAL(18, 2) NULL,
        DiscountType         NVARCHAR(10)   NOT NULL CONSTRAINT DF_ProductBundleConfig_DiscountType DEFAULT (N'none'),          -- none | percent | fixed
        DiscountValue        DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ProductBundleConfig_DiscountValue DEFAULT (0),
        OosBehavior          NVARCHAR(20)   NOT NULL CONSTRAINT DF_ProductBundleConfig_OosBehavior DEFAULT (N'unavailable'),    -- unavailable | swap
        Layout               NVARCHAR(10)   NOT NULL CONSTRAINT DF_ProductBundleConfig_Layout DEFAULT (N'grid'),                -- grid | list
        CartDisplay          NVARCHAR(40)   NOT NULL CONSTRAINT DF_ProductBundleConfig_CartDisplay DEFAULT (N'name_with_components'), -- name_with_components | name_only | line
        InvoiceDisplay       NVARCHAR(20)   NOT NULL CONSTRAINT DF_ProductBundleConfig_InvoiceDisplay DEFAULT (N'bundle'),      -- bundle | components
        HidePriceLabels      BIT            NOT NULL CONSTRAINT DF_ProductBundleConfig_HidePriceLabels DEFAULT (0),
        ReweighPrice         BIT            NOT NULL CONSTRAINT DF_ProductBundleConfig_ReweighPrice DEFAULT (0),
        ShowComponentsInDesc BIT            NOT NULL CONSTRAINT DF_ProductBundleConfig_ShowComponentsInDesc DEFAULT (0),
        UpdatedDate          DATETIME2(0)   NULL,
        CONSTRAINT FK_ProductBundleConfig_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.ProductBundleComponent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBundleComponent (
        Id                 INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductBundleComponent PRIMARY KEY,
        BundleProductId    INT            NOT NULL,
        ComponentProductId INT            NOT NULL,
        ComponentVariantId INT            NULL,
        Qty                DECIMAL(18, 4) NOT NULL,
        SortOrder          INT            NOT NULL CONSTRAINT DF_ProductBundleComponent_SortOrder DEFAULT (0),
        Swappable          BIT            NOT NULL CONSTRAINT DF_ProductBundleComponent_Swappable DEFAULT (0),
        Description        NVARCHAR(500)  NULL,
        ComponentKey       NVARCHAR(40)   NOT NULL CONSTRAINT DF_ProductBundleComponent_ComponentKey DEFAULT (N''),  -- stable key ("c" + Id), set after insert
        Unit               NVARCHAR(10)   NULL,   -- read-only mirror of what Woo reports back
        Mode               NVARCHAR(20)   NULL,
        UnitWeightKg       DECIMAL(18, 4) NULL,
        IsDeleted          BIT            NOT NULL CONSTRAINT DF_ProductBundleComponent_IsDeleted DEFAULT (0),
        CONSTRAINT FK_ProductBundleComponent_BundleProduct    FOREIGN KEY (BundleProductId)    REFERENCES dbo.Product (Id),
        CONSTRAINT FK_ProductBundleComponent_ComponentProduct FOREIGN KEY (ComponentProductId) REFERENCES dbo.Product (Id),
        CONSTRAINT FK_ProductBundleComponent_ComponentVariant FOREIGN KEY (ComponentVariantId) REFERENCES dbo.ProductVariant (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ProductBundleComponent_Bundle_IsDeleted
        ON dbo.ProductBundleComponent (BundleProductId, IsDeleted);
END
GO

IF OBJECT_ID(N'dbo.ProductBundleComponentSwap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBundleComponentSwap (
        Id            INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductBundleComponentSwap PRIMARY KEY,
        ComponentId   INT            NOT NULL,
        SwapProductId INT            NOT NULL,
        SwapVariantId INT            NULL,
        Surcharge     DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ProductBundleComponentSwap_Surcharge DEFAULT (0),
        SortOrder     INT            NOT NULL CONSTRAINT DF_ProductBundleComponentSwap_SortOrder DEFAULT (0),
        IsDeleted     BIT            NOT NULL CONSTRAINT DF_ProductBundleComponentSwap_IsDeleted DEFAULT (0),
        CONSTRAINT FK_ProductBundleComponentSwap_Component   FOREIGN KEY (ComponentId)   REFERENCES dbo.ProductBundleComponent (Id),
        CONSTRAINT FK_ProductBundleComponentSwap_SwapProduct FOREIGN KEY (SwapProductId) REFERENCES dbo.Product (Id),
        CONSTRAINT FK_ProductBundleComponentSwap_SwapVariant FOREIGN KEY (SwapVariantId) REFERENCES dbo.ProductVariant (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ProductBundleComponentSwap_Component_IsDeleted
        ON dbo.ProductBundleComponentSwap (ComponentId, IsDeleted);
END
GO

-- An alternative may carry its OWN quantity per bundle (its unit); NULL = inherit the slot quantity.
IF COL_LENGTH(N'dbo.ProductBundleComponentSwap', N'Qty') IS NULL
    ALTER TABLE dbo.ProductBundleComponentSwap ADD Qty DECIMAL(18, 4) NULL;
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: OrderItem_AddBundleColumns.sql
-- =============================================================================

-- Bundles (מארזים): order-line linkage. Parent line = the bundle (ProductId = BundleProductId);
-- child lines = its components (ParentOrderItemId = parent). No cascade on the self-reference.
-- Spec: BUNDLES_SYNC_SPEC.md §2. Idempotent.

IF COL_LENGTH(N'dbo.OrderItem', N'BundleProductId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD BundleProductId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'ParentOrderItemId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD ParentOrderItemId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'BundleComponentId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD BundleComponentId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'BundleComponentIndex') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD BundleComponentIndex INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'SwappedFromProductId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD SwappedFromProductId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'SwapSurcharge') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD SwapSurcharge DECIMAL(18, 2) NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'WooLineItemId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD WooLineItemId INT NULL;
END
GO

-- Self-reference parent → child. NO cascade: a parent delete is a soft delete handled in code.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItem_ParentOrderItem')
BEGIN
    ALTER TABLE dbo.OrderItem WITH CHECK
        ADD CONSTRAINT FK_OrderItem_ParentOrderItem
        FOREIGN KEY (ParentOrderItemId) REFERENCES dbo.OrderItem (Id);
END
GO

-- Slot reference (requires ProductBundle_CreateTables.sql first).
IF OBJECT_ID(N'dbo.ProductBundleComponent', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItem_BundleComponent')
BEGIN
    ALTER TABLE dbo.OrderItem WITH CHECK
        ADD CONSTRAINT FK_OrderItem_BundleComponent
        FOREIGN KEY (BundleComponentId) REFERENCES dbo.ProductBundleComponent (Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItem_ParentOrderItemId' AND object_id = OBJECT_ID(N'dbo.OrderItem'))
BEGIN
    CREATE INDEX IX_OrderItem_ParentOrderItemId
        ON dbo.OrderItem (ParentOrderItemId)
        WHERE ParentOrderItemId IS NOT NULL;
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- SOURCE: ProductStatus_AddDraft.sql
-- =============================================================================

-- The bundle editor offers "טיוטה" (draft). Without a 'draft' lookup row the status saved as NULL and the
-- WooCommerce sync published the bundle. Idempotent.
IF NOT EXISTS (SELECT 1 FROM dbo.ProductStatus WHERE Name = N'draft')
BEGIN
    INSERT INTO dbo.ProductStatus (Name, IsDeleted) VALUES (N'draft', 0);
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- SOURCE: Product_AddPrintName.sql
-- =============================================================================

-- Product.PrintName: optional name printed on the ORDER-ENTRY voucher instead of the catalog name
-- (e.g. Arabic text for the pickers). NULL = the catalog name. Idempotent.
IF COL_LENGTH(N'dbo.Product', N'PrintName') IS NULL
    ALTER TABLE dbo.Product ADD PrintName NVARCHAR(300) NULL;
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- SOURCE: Site_AddScanCompletesReadyOrder.sql
-- =============================================================================

-- Site.ScanCompletesReadyOrder: scanning a READY order's voucher barcode completes the order instead of
-- opening its window. NULL / 0 = open the window (the behaviour so far). Idempotent.
IF COL_LENGTH(N'dbo.Site', N'ScanCompletesReadyOrder') IS NULL
    ALTER TABLE dbo.Site ADD ScanCompletesReadyOrder BIT NULL;
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- SOURCE: Site_AddProductPrintNameEnabled.sql
-- =============================================================================

-- Site.ProductPrintNameEnabled: opt-in per site for Product.PrintName (hidden + ignored on prints while off).
IF COL_LENGTH(N'dbo.Site', N'ProductPrintNameEnabled') IS NULL
    ALTER TABLE dbo.Site ADD ProductPrintNameEnabled BIT NULL;
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- COMMIT
-- =============================================================================
IF @@TRANCOUNT > 0 COMMIT TRANSACTION;
SET NOEXEC OFF;
PRINT N'Bundles production install committed successfully.';
GO
