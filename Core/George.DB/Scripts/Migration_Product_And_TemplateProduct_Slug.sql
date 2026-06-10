-- Permalink slug for WooCommerce product URL (post_name). Mirrors Yoast "slug" field.
IF COL_LENGTH(N'dbo.Product', N'Slug') IS NULL
BEGIN
    ALTER TABLE dbo.Product ADD Slug NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'dbo.TemplateProduct', N'Slug') IS NULL
BEGIN
    ALTER TABLE dbo.TemplateProduct ADD Slug NVARCHAR(200) NULL;
END
GO
