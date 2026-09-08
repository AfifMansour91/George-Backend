-- ProductSiteWooSyncStatus: outcome of the LAST WooCommerce sync of a product to one site's store.
-- Written by every product sync (save-triggered or sync-all); the product page shows the shop a
-- red banner when the last attempt failed (before: log line only - Meshek Basar PT 8/9, "עוף טחון"
-- could not sync for a week and nobody knew).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF OBJECT_ID(N'dbo.ProductSiteWooSyncStatus', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteWooSyncStatus]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductSiteWooSyncStatus] PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [SiteId] INT NOT NULL,
        [LastSyncAt] DATETIME2(7) NOT NULL,
        [Success] BIT NOT NULL,
        [Action] NVARCHAR(20) NULL,
        [WooCommerceProductId] INT NULL,
        [Error] NVARCHAR(1000) NULL,
        CONSTRAINT [FK_ProductSiteWooSyncStatus_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product]([Id]),
        CONSTRAINT [FK_ProductSiteWooSyncStatus_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site]([Id])
    );
    CREATE UNIQUE INDEX [UX_ProductSiteWooSyncStatus_Product_Site]
        ON [dbo].[ProductSiteWooSyncStatus]([ProductId], [SiteId]);
    PRINT 'Created ProductSiteWooSyncStatus';
END
GO
