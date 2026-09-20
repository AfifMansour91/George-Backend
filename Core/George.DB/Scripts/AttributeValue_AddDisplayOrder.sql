-- Attribute value (term) manual order - drag and drop on the attributes screen (2026-09-20).
--   DisplayOrder - position of the value inside its attribute (lower = first). NULL = never ordered:
--                  the value sorts after the ordered ones, alphabetically (the behavior before this column).
--                  Drives the value/variation order in Giorgio and the WooCommerce term menu_order.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.AttributeValue', N'DisplayOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[AttributeValue] ADD [DisplayOrder] INT NULL;
    PRINT 'Added DisplayOrder to AttributeValue';
END
GO

-- "גודל" / "Size" backfill. Product saves used to skip creating a site Attribute for the size option, so sites whose
-- size attribute was never created by the Woo sync (3 sites on prod, 2026-09-20) have nothing to order on the
-- attributes screen. Creates the missing attribute per site + the size values their live products use.
-- New values get DisplayOrder NULL (never ordered). Safe to re-run.
-- N'גודל' built from code points so the script doesn't depend on the file encoding.
DECLARE @SizeHe NVARCHAR(10) = NCHAR(0x05D2) + NCHAR(0x05D5) + NCHAR(0x05D3) + NCHAR(0x05DC);
INSERT INTO [dbo].[Attribute] ([IsDeleted], [GuidId], [CreationTime], [Name], [SiteId])
SELECT 0, NEWID(), SYSUTCDATETIME(), src.Name, src.SiteId
FROM (
    SELECT DISTINCT ps.SiteId, LTRIM(RTRIM(po.Name)) AS Name
    FROM [dbo].[ProductOption] po
    JOIN [dbo].[Product] p ON p.Id = po.ProductId AND p.IsDeleted = 0
    JOIN [dbo].[ProductSite] ps ON ps.ProductId = p.Id
    WHERE po.IsDeleted = 0 AND LTRIM(RTRIM(po.Name)) IN (@SizeHe, N'Size')
) src
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Attribute] a
    WHERE a.IsDeleted = 0 AND a.SiteId = src.SiteId AND a.Name = src.Name
);
PRINT CONCAT('Size attributes created: ', @@ROWCOUNT);
GO

DECLARE @SizeHe NVARCHAR(10) = NCHAR(0x05D2) + NCHAR(0x05D5) + NCHAR(0x05D3) + NCHAR(0x05DC);
INSERT INTO [dbo].[AttributeValue] ([AttributeId], [Value])
SELECT DISTINCT a.Id, LTRIM(RTRIM(pov.Value))
FROM [dbo].[Attribute] a
JOIN [dbo].[ProductSite] ps ON ps.SiteId = a.SiteId
JOIN [dbo].[Product] p ON p.Id = ps.ProductId AND p.IsDeleted = 0
JOIN [dbo].[ProductOption] po ON po.ProductId = p.Id AND po.IsDeleted = 0 AND LTRIM(RTRIM(po.Name)) = a.Name
JOIN [dbo].[ProductOptionValue] pov ON pov.ProductOptionId = po.Id
WHERE a.IsDeleted = 0 AND a.Name IN (@SizeHe, N'Size')
  AND LTRIM(RTRIM(pov.Value)) <> N''
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[AttributeValue] av
      WHERE av.AttributeId = a.Id AND av.Value = LTRIM(RTRIM(pov.Value))
  );
PRINT CONCAT('Size attribute values added: ', @@ROWCOUNT);
GO
