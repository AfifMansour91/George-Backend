-- Add DisplayOrder to Product and TemplateProduct for drag-and-drop list ordering
-- Run this script on your database before using the Order API.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Product') AND name = 'DisplayOrder')
BEGIN
    ALTER TABLE [dbo].[Product] ADD [DisplayOrder] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TemplateProduct') AND name = 'DisplayOrder')
BEGIN
    ALTER TABLE [dbo].[TemplateProduct] ADD [DisplayOrder] INT NULL;
END
GO
