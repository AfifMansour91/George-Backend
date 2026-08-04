-- DataFix: Zano Dagim domain change  zano-p.deliz.co.il  ->  zano-dagim.co.il  (03/08/2026)
-- The WooCommerce store moved to a new domain. All image/config URLs stored in George still point
-- at the old host, so product images fail to load and webhooks/sync hit a dead domain.
-- Replaces the host substring everywhere it can appear (works for both http:// and https://).
-- Idempotent: after the first run nothing matches the old host anymore.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @OldHost NVARCHAR(200) = N'zano-p.deliz.co.il';
DECLARE @NewHost NVARCHAR(200) = N'zano-dagim.co.il';
DECLARE @Match   NVARCHAR(210) = N'%' + @OldHost + N'%';

BEGIN TRANSACTION;

------------------------------------------------------------------------------
-- 0. Site config: Woo API base + promotion webhook + Cardcom css/logo
------------------------------------------------------------------------------
SELECT [Id], [Name], [WooCommerceUrl], [PromotionWebhookUrl], [CardcomCssUrl], [CardcomLogoUrl]
FROM [dbo].[Site] WHERE [WooCommerceUrl] LIKE @Match OR [PromotionWebhookUrl] LIKE @Match
   OR [CardcomCssUrl] LIKE @Match OR [CardcomLogoUrl] LIKE @Match;

UPDATE [dbo].[Site] SET
    [WooCommerceUrl]      = REPLACE([WooCommerceUrl], @OldHost, @NewHost),
    [PromotionWebhookUrl] = REPLACE([PromotionWebhookUrl], @OldHost, @NewHost),
    [CardcomCssUrl]       = REPLACE([CardcomCssUrl], @OldHost, @NewHost),
    [CardcomLogoUrl]      = REPLACE([CardcomLogoUrl], @OldHost, @NewHost)
WHERE [WooCommerceUrl] LIKE @Match OR [PromotionWebhookUrl] LIKE @Match
   OR [CardcomCssUrl] LIKE @Match OR [CardcomLogoUrl] LIKE @Match;
PRINT 'Site rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 1. ProductImage (PK = ProductId+Url): drop old-host rows whose replaced URL
--    already exists for the same product, then rewrite the rest.
------------------------------------------------------------------------------
DELETE pi
FROM [dbo].[ProductImage] pi
WHERE pi.[Url] LIKE @Match
  AND EXISTS (SELECT 1 FROM [dbo].[ProductImage] x
              WHERE x.[ProductId] = pi.[ProductId]
                AND x.[Url] = REPLACE(pi.[Url], @OldHost, @NewHost));
PRINT 'ProductImage duplicate old-host rows deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[ProductImage] SET [Url] = REPLACE([Url], @OldHost, @NewHost) WHERE [Url] LIKE @Match;
PRINT 'ProductImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 2. Per-site image overrides + variant images
------------------------------------------------------------------------------
UPDATE [dbo].[ProductSiteImage] SET [Url] = REPLACE([Url], @OldHost, @NewHost) WHERE [Url] LIKE @Match;
PRINT 'ProductSiteImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[ProductVariant] SET [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost) WHERE [ImageUrl] LIKE @Match;
PRINT 'ProductVariant rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 3. Categories / brands (account-level and global)
------------------------------------------------------------------------------
UPDATE [dbo].[Category] SET
    [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost),
    [IconUrl]  = REPLACE([IconUrl], @OldHost, @NewHost)
WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
PRINT 'Category rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

IF OBJECT_ID(N'[dbo].[GlobalCategory]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[GlobalCategory] SET
        [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost),
        [IconUrl]  = REPLACE([IconUrl], @OldHost, @NewHost)
    WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
    PRINT 'GlobalCategory rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
END

IF OBJECT_ID(N'[dbo].[Brand]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[Brand] SET
        [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost),
        [IconUrl]  = REPLACE([IconUrl], @OldHost, @NewHost)
    WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
    PRINT 'Brand rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
END

IF OBJECT_ID(N'[dbo].[GlobalBrand]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[GlobalBrand] SET
        [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost),
        [IconUrl]  = REPLACE([IconUrl], @OldHost, @NewHost)
    WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
    PRINT 'GlobalBrand rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
END

------------------------------------------------------------------------------
-- 4. Media library + account/kiosk logos
------------------------------------------------------------------------------
UPDATE [dbo].[Media] SET [Url] = REPLACE([Url], @OldHost, @NewHost) WHERE [Url] LIKE @Match;
PRINT 'Media rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[Account] SET [LogoUrl] = REPLACE([LogoUrl], @OldHost, @NewHost) WHERE [LogoUrl] LIKE @Match;
PRINT 'Account rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

IF OBJECT_ID(N'[dbo].[KioskSettings]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[KioskSettings] SET [KioskLogoUrl] = REPLACE([KioskLogoUrl], @OldHost, @NewHost) WHERE [KioskLogoUrl] LIKE @Match;
    PRINT 'KioskSettings rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
END

------------------------------------------------------------------------------
-- 5. Template catalog (PK of TemplateProductImage = TemplateProductId+Url)
------------------------------------------------------------------------------
DELETE tpi
FROM [dbo].[TemplateProductImage] tpi
WHERE tpi.[Url] LIKE @Match
  AND EXISTS (SELECT 1 FROM [dbo].[TemplateProductImage] x
              WHERE x.[TemplateProductId] = tpi.[TemplateProductId]
                AND x.[Url] = REPLACE(tpi.[Url], @OldHost, @NewHost));
PRINT 'TemplateProductImage duplicate old-host rows deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[TemplateProductImage] SET [Url] = REPLACE([Url], @OldHost, @NewHost) WHERE [Url] LIKE @Match;
PRINT 'TemplateProductImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[TemplateProductVariant] SET [ImageUrl] = REPLACE([ImageUrl], @OldHost, @NewHost) WHERE [ImageUrl] LIKE @Match;
PRINT 'TemplateProductVariant rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

COMMIT TRANSACTION;

-- Verification: nothing should reference the old host anymore.
SELECT 'ProductImage' AS T, COUNT(*) AS Remaining FROM [dbo].[ProductImage] WHERE [Url] LIKE @Match
UNION ALL SELECT 'ProductSiteImage', COUNT(*) FROM [dbo].[ProductSiteImage] WHERE [Url] LIKE @Match
UNION ALL SELECT 'ProductVariant', COUNT(*) FROM [dbo].[ProductVariant] WHERE [ImageUrl] LIKE @Match
UNION ALL SELECT 'Media', COUNT(*) FROM [dbo].[Media] WHERE [Url] LIKE @Match
UNION ALL SELECT 'Site', COUNT(*) FROM [dbo].[Site] WHERE [WooCommerceUrl] LIKE @Match OR [PromotionWebhookUrl] LIKE @Match;
