-- Migration: Add ProductBrand join table (many-to-many Product <-> Brand) + backfill
-- Date: 2026-05-07
-- Description: Mirrors ProductCategory. Has IsPrimary so we can preserve "the" brand pointer
--              that today lives in Product.BrandId. The existing Product.BrandId column is left
--              in place for one release; new code should write through ProductBrand.
--
-- Idempotent: rerunning won't duplicate rows.

------------------------------------------------------------
-- 1. Table
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductBrand' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[ProductBrand](
        [ProductId] [int] NOT NULL,
        [BrandId]   [int] NOT NULL,
        [IsPrimary] [bit] NOT NULL,
     CONSTRAINT [PK_ProductBrand] PRIMARY KEY CLUSTERED
    (
        [ProductId] ASC,
        [BrandId]   ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];

    ALTER TABLE [dbo].[ProductBrand] ADD CONSTRAINT [DF_ProductBrand_IsPrimary] DEFAULT (0) FOR [IsPrimary];

    PRINT 'Created ProductBrand table';
END
ELSE
BEGIN
    PRINT 'ProductBrand table already exists';
END
GO

------------------------------------------------------------
-- 2. Index on BrandId
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductBrand_BrandId' AND object_id = OBJECT_ID(N'[dbo].[ProductBrand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProductBrand_BrandId] ON [dbo].[ProductBrand]([BrandId] ASC) ON [PRIMARY];
    PRINT 'Created index IX_ProductBrand_BrandId';
END
GO

------------------------------------------------------------
-- 3. Foreign keys
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductBrand_Brand' AND parent_object_id = OBJECT_ID(N'[dbo].[ProductBrand]'))
BEGIN
    ALTER TABLE [dbo].[ProductBrand] WITH CHECK ADD CONSTRAINT [FK_ProductBrand_Brand]
        FOREIGN KEY([BrandId]) REFERENCES [dbo].[Brand]([Id]);
    ALTER TABLE [dbo].[ProductBrand] CHECK CONSTRAINT [FK_ProductBrand_Brand];
    PRINT 'Added FK_ProductBrand_Brand';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductBrand_Product' AND parent_object_id = OBJECT_ID(N'[dbo].[ProductBrand]'))
BEGIN
    ALTER TABLE [dbo].[ProductBrand] WITH CHECK ADD CONSTRAINT [FK_ProductBrand_Product]
        FOREIGN KEY([ProductId]) REFERENCES [dbo].[Product]([Id]);
    ALTER TABLE [dbo].[ProductBrand] CHECK CONSTRAINT [FK_ProductBrand_Product];
    PRINT 'Added FK_ProductBrand_Product';
END
GO

------------------------------------------------------------
-- 4. Backfill: copy Product.BrandId -> ProductBrand
------------------------------------------------------------
-- For every Product that has a non-null BrandId and isn't soft-deleted, ensure a row in
-- ProductBrand with IsPrimary = 1. This is idempotent: rows that already exist are skipped.

INSERT INTO [dbo].[ProductBrand] ([ProductId], [BrandId], [IsPrimary])
SELECT p.[Id], p.[BrandId], 1
FROM [dbo].[Product] p
WHERE p.[BrandId] IS NOT NULL
  AND ISNULL(p.[IsDeleted], 0) = 0
  AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[ProductBrand] pb
        WHERE pb.[ProductId] = p.[Id]
          AND pb.[BrandId]   = p.[BrandId]
  );

DECLARE @rowsCopied int = @@ROWCOUNT;
PRINT CONCAT('Backfilled ', @rowsCopied, ' ProductBrand rows from Product.BrandId.');
GO

PRINT 'Migration_AddProductBrandTable complete.';
