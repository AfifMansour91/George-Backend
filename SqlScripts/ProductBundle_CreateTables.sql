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
