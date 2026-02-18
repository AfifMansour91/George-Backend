-- Migration: Add identity long primary key to ProductImage and TemplateProductImage
-- Description: Enables efficient in-place UPDATE of Url when media is saved to our storage
--              (no delete+re-insert). Keeps (ProductId, Url) / (TemplateProductId, Url) unique.

USE [George]
GO

-- ========== ProductImage ==========
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductImage]') AND name = N'Id')
BEGIN
    ALTER TABLE [dbo].[ProductImage] ADD [Id] [bigint] IDENTITY(1,1) NOT NULL
    PRINT 'Added ProductImage.Id'
END
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[ProductImage]') AND name = N'PK_ProductImage')
BEGIN
    ALTER TABLE [dbo].[ProductImage] DROP CONSTRAINT [PK_ProductImage]
    PRINT 'Dropped PK_ProductImage'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[ProductImage]') AND name = N'PK_ProductImage')
BEGIN
    ALTER TABLE [dbo].[ProductImage] ADD CONSTRAINT [PK_ProductImage] PRIMARY KEY CLUSTERED ([Id] ASC)
    PRINT 'Created PK_ProductImage on Id'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductImage_ProductId_Url' AND object_id = OBJECT_ID(N'[dbo].[ProductImage]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ProductImage_ProductId_Url] ON [dbo].[ProductImage]([ProductId] ASC, [Url] ASC)
    PRINT 'Created unique IX_ProductImage_ProductId_Url'
END
GO

-- ========== TemplateProductImage ==========
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]') AND name = N'Id')
BEGIN
    ALTER TABLE [dbo].[TemplateProductImage] ADD [Id] [bigint] IDENTITY(1,1) NOT NULL
    PRINT 'Added TemplateProductImage.Id'
END
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]') AND name = N'PK_TemplateProductImage')
BEGIN
    ALTER TABLE [dbo].[TemplateProductImage] DROP CONSTRAINT [PK_TemplateProductImage]
    PRINT 'Dropped PK_TemplateProductImage'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]') AND name = N'PK_TemplateProductImage')
BEGIN
    ALTER TABLE [dbo].[TemplateProductImage] ADD CONSTRAINT [PK_TemplateProductImage] PRIMARY KEY CLUSTERED ([Id] ASC)
    PRINT 'Created PK_TemplateProductImage on Id'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemplateProductImage_TemplateProductId_Url' AND object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_TemplateProductImage_TemplateProductId_Url] ON [dbo].[TemplateProductImage]([TemplateProductId] ASC, [Url] ASC)
    PRINT 'Created unique IX_TemplateProductImage_TemplateProductId_Url'
END
GO

PRINT 'Migration_ProductImageTemplateProductImage_IdentityPk completed successfully'
GO
