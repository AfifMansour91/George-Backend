-- Migration: Add MediaId FK to ProductImage and TemplateProductImage
-- Description: Link product images to account media so import uses existing media
--              and deleting media clears the reference on products.

USE [George]
GO

-- ProductImage.MediaId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductImage]') AND name = N'MediaId')
BEGIN
    ALTER TABLE [dbo].[ProductImage] ADD [MediaId] [int] NULL
    PRINT 'Added ProductImage.MediaId'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_ProductImage_Media')
BEGIN
    ALTER TABLE [dbo].[ProductImage] WITH CHECK ADD CONSTRAINT [FK_ProductImage_Media] FOREIGN KEY([MediaId])
    REFERENCES [dbo].[Media] ([Id])
    ON DELETE SET NULL
    ALTER TABLE [dbo].[ProductImage] CHECK CONSTRAINT [FK_ProductImage_Media]
    PRINT 'Added FK_ProductImage_Media'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ProductImage_MediaId' AND object_id = OBJECT_ID(N'[dbo].[ProductImage]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProductImage_MediaId] ON [dbo].[ProductImage]([MediaId] ASC)
    PRINT 'Created IX_ProductImage_MediaId'
END
GO

-- TemplateProductImage.MediaId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]') AND name = N'MediaId')
BEGIN
    ALTER TABLE [dbo].[TemplateProductImage] ADD [MediaId] [int] NULL
    PRINT 'Added TemplateProductImage.MediaId'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_TemplateProductImage_Media')
BEGIN
    ALTER TABLE [dbo].[TemplateProductImage] WITH CHECK ADD CONSTRAINT [FK_TemplateProductImage_Media] FOREIGN KEY([MediaId])
    REFERENCES [dbo].[Media] ([Id])
    ON DELETE SET NULL
    ALTER TABLE [dbo].[TemplateProductImage] CHECK CONSTRAINT [FK_TemplateProductImage_Media]
    PRINT 'Added FK_TemplateProductImage_Media'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_TemplateProductImage_MediaId' AND object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TemplateProductImage_MediaId] ON [dbo].[TemplateProductImage]([MediaId] ASC)
    PRINT 'Created IX_TemplateProductImage_MediaId'
END
GO

PRINT 'Migration_AddProductImageMediaId completed successfully'
GO
