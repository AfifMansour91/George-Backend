-- Bundles (מארזים): order-line linkage. Parent line = the bundle (ProductId = BundleProductId);
-- child lines = its components (ParentOrderItemId = parent). No cascade on the self-reference.
-- Spec: BUNDLES_SYNC_SPEC.md §2. Idempotent.
-- The filtered index below requires QUOTED_IDENTIFIER ON (sqlcmd defaults it OFF; SSMS defaults it ON).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

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
