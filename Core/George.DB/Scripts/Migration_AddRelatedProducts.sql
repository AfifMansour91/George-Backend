-- Migration: Add related products (נלווים) join tables for TemplateProduct and Product
-- Date: 2026-02-13
-- Description: TemplateProductRelated and ProductRelated store related/accessory product links.

USE [George]
GO

-- TemplateProductRelated: many-to-many self-referential for template products
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProductRelated]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TemplateProductRelated](
        [TemplateProductId] [int] NOT NULL,
        [RelatedTemplateProductId] [int] NOT NULL,
        CONSTRAINT [PK_TemplateProductRelated] PRIMARY KEY CLUSTERED ([TemplateProductId] ASC, [RelatedTemplateProductId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]
    PRINT 'Created TemplateProductRelated table'
END
ELSE
BEGIN
    PRINT 'TemplateProductRelated table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TPR_TemplateProduct')
BEGIN
    ALTER TABLE [dbo].[TemplateProductRelated] WITH CHECK ADD CONSTRAINT [FK_TPR_TemplateProduct] FOREIGN KEY([TemplateProductId])
    REFERENCES [dbo].[TemplateProduct] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[TemplateProductRelated] CHECK CONSTRAINT [FK_TPR_TemplateProduct]
    PRINT 'Added FK_TPR_TemplateProduct'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TPR_RelatedTemplateProduct')
BEGIN
    ALTER TABLE [dbo].[TemplateProductRelated] WITH CHECK ADD CONSTRAINT [FK_TPR_RelatedTemplateProduct] FOREIGN KEY([RelatedTemplateProductId])
    REFERENCES [dbo].[TemplateProduct] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[TemplateProductRelated] CHECK CONSTRAINT [FK_TPR_RelatedTemplateProduct]
    PRINT 'Added FK_TPR_RelatedTemplateProduct'
END
GO

-- ProductRelated: many-to-many self-referential for account products
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductRelated]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProductRelated](
        [ProductId] [int] NOT NULL,
        [RelatedProductId] [int] NOT NULL,
        CONSTRAINT [PK_ProductRelated] PRIMARY KEY CLUSTERED ([ProductId] ASC, [RelatedProductId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]
    PRINT 'Created ProductRelated table'
END
ELSE
BEGIN
    PRINT 'ProductRelated table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ProductRelated_Product')
BEGIN
    ALTER TABLE [dbo].[ProductRelated] WITH CHECK ADD CONSTRAINT [FK_ProductRelated_Product] FOREIGN KEY([ProductId])
    REFERENCES [dbo].[Product] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[ProductRelated] CHECK CONSTRAINT [FK_ProductRelated_Product]
    PRINT 'Added FK_ProductRelated_Product'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ProductRelated_RelatedProduct')
BEGIN
    ALTER TABLE [dbo].[ProductRelated] WITH CHECK ADD CONSTRAINT [FK_ProductRelated_RelatedProduct] FOREIGN KEY([RelatedProductId])
    REFERENCES [dbo].[Product] ([Id])
    ON DELETE NO ACTION
    ALTER TABLE [dbo].[ProductRelated] CHECK CONSTRAINT [FK_ProductRelated_RelatedProduct]
    PRINT 'Added FK_ProductRelated_RelatedProduct'
END
GO

PRINT 'Migration_AddRelatedProducts completed successfully'
GO
