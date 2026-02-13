-- Migration: Add complementary products (מוצרים משלימים) join tables for TemplateProduct and Product
-- Date: 2026-02-13
-- Description: TemplateProductComplementary and ProductComplementary store complementary product links.

USE [George]
GO

-- TemplateProductComplementary: many-to-many self-referential for template products
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProductComplementary]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TemplateProductComplementary](
        [TemplateProductId] [int] NOT NULL,
        [ComplementaryTemplateProductId] [int] NOT NULL,
        CONSTRAINT [PK_TemplateProductComplementary] PRIMARY KEY CLUSTERED ([TemplateProductId] ASC, [ComplementaryTemplateProductId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]
    PRINT 'Created TemplateProductComplementary table'
END
ELSE
BEGIN
    PRINT 'TemplateProductComplementary table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TPComplementary_TemplateProduct')
BEGIN
    ALTER TABLE [dbo].[TemplateProductComplementary] WITH CHECK ADD CONSTRAINT [FK_TPComplementary_TemplateProduct] FOREIGN KEY([TemplateProductId])
    REFERENCES [dbo].[TemplateProduct] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[TemplateProductComplementary] CHECK CONSTRAINT [FK_TPComplementary_TemplateProduct]
    PRINT 'Added FK_TPComplementary_TemplateProduct'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TPComplementary_ComplementaryTemplateProduct')
BEGIN
    ALTER TABLE [dbo].[TemplateProductComplementary] WITH CHECK ADD CONSTRAINT [FK_TPComplementary_ComplementaryTemplateProduct] FOREIGN KEY([ComplementaryTemplateProductId])
    REFERENCES [dbo].[TemplateProduct] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[TemplateProductComplementary] CHECK CONSTRAINT [FK_TPComplementary_ComplementaryTemplateProduct]
    PRINT 'Added FK_TPComplementary_ComplementaryTemplateProduct'
END
GO

-- ProductComplementary: many-to-many self-referential for account products
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductComplementary]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProductComplementary](
        [ProductId] [int] NOT NULL,
        [ComplementaryProductId] [int] NOT NULL,
        CONSTRAINT [PK_ProductComplementary] PRIMARY KEY CLUSTERED ([ProductId] ASC, [ComplementaryProductId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]
    PRINT 'Created ProductComplementary table'
END
ELSE
BEGIN
    PRINT 'ProductComplementary table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ProductComplementary_Product')
BEGIN
    ALTER TABLE [dbo].[ProductComplementary] WITH CHECK ADD CONSTRAINT [FK_ProductComplementary_Product] FOREIGN KEY([ProductId])
    REFERENCES [dbo].[Product] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[ProductComplementary] CHECK CONSTRAINT [FK_ProductComplementary_Product]
    PRINT 'Added FK_ProductComplementary_Product'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ProductComplementary_ComplementaryProduct')
BEGIN
    ALTER TABLE [dbo].[ProductComplementary] WITH CHECK ADD CONSTRAINT [FK_ProductComplementary_ComplementaryProduct] FOREIGN KEY([ComplementaryProductId])
    REFERENCES [dbo].[Product] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[ProductComplementary] CHECK CONSTRAINT [FK_ProductComplementary_ComplementaryProduct]
    PRINT 'Added FK_ProductComplementary_ComplementaryProduct'
END
GO

PRINT 'Migration_AddComplementaryProducts completed successfully'
GO
