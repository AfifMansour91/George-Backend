-- Migration: Backfill MediaId on existing ProductImage and TemplateProductImage
-- Description: Sets MediaId where Url matches an existing Media record.
--              ProductImage: only links to Media that belongs to the product's account (AccountMedia).
--              TemplateProductImage: links to any Media with matching Url (no account; takes one match).
-- Run this after Migration_AddProductImageMediaId.sql when the MediaId column exists.

USE [George]
GO

-- Ensure ProductImage has MediaId column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductImage]') AND name = N'MediaId')
BEGIN
    PRINT 'ProductImage.MediaId column missing. Run Migration_AddProductImageMediaId.sql first.'
END
ELSE
BEGIN
    -- ProductImage: set MediaId where Url matches Media in the same account (via AccountMedia); one Media per row (lowest Id)
    UPDATE pi
    SET pi.MediaId = m.Id
    FROM [dbo].[ProductImage] pi
    INNER JOIN [dbo].[Product] p ON p.Id = pi.ProductId AND p.AccountId IS NOT NULL
    CROSS APPLY (
        SELECT TOP 1 m2.Id
        FROM [dbo].[Media] m2
        INNER JOIN [dbo].[AccountMedia] am ON am.MediaId = m2.Id AND am.AccountId = p.AccountId
        WHERE m2.Url = pi.Url AND m2.IsDeleted = 0
        ORDER BY m2.Id
    ) m
    WHERE pi.MediaId IS NULL AND pi.Url IS NOT NULL;

    PRINT 'Backfilled ProductImage.MediaId: ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' row(s)'
END
GO

-- Ensure TemplateProductImage has MediaId column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProductImage]') AND name = N'MediaId')
BEGIN
    PRINT 'TemplateProductImage.MediaId column missing. Run Migration_AddProductImageMediaId.sql first.'
END
ELSE
BEGIN
    -- TemplateProductImage: set MediaId to any Media with matching Url (deterministic: lowest Id)
    UPDATE tpi
    SET tpi.MediaId = m.Id
    FROM [dbo].[TemplateProductImage] tpi
    CROSS APPLY (
        SELECT TOP 1 Id
        FROM [dbo].[Media]
        WHERE Url = tpi.Url AND IsDeleted = 0
        ORDER BY Id
    ) m
    WHERE tpi.MediaId IS NULL AND tpi.Url IS NOT NULL;

    PRINT 'Backfilled TemplateProductImage.MediaId: ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' row(s)'
END
GO

PRINT 'Migration_BackfillProductImageMediaId completed'
GO
