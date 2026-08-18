-- DataFix: stored file URLs http://api.storeos.co.il -> https://api.storeos.co.il  (19/08/2026)
-- Production appsettings.json had FileStorage:StorageLocalExternalBasePath configured with "http://",
-- so every uploaded file's url was STORED with http. Pages served over https (kiosk, storefront)
-- treat http media as mixed content — the Dubi-Dagim kiosk showed an HTTPS warning for its home
-- video. Code now upgrades the scheme on the way in (FileHelper.UpgradeInsecureExternalUrl); this
-- fixes the ~10K rows already stored with http.
-- Idempotent: after the first run nothing matches the http prefix anymore.

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Required for updating tables carrying filtered indexes (sqlcmd defaults it OFF).
SET QUOTED_IDENTIFIER ON;

DECLARE @OldPrefix NVARCHAR(100) = N'http://api.storeos.co.il';
DECLARE @NewPrefix NVARCHAR(100) = N'https://api.storeos.co.il';
DECLARE @Match     NVARCHAR(110) = @OldPrefix + N'%';

BEGIN TRANSACTION;

------------------------------------------------------------------------------
-- 1. Media (kiosk videos, library images)
------------------------------------------------------------------------------
UPDATE [dbo].[Media]
SET [Url] = STUFF([Url], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [Url] LIKE @Match;
PRINT 'Media rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 2. ProductImage — unique index on (ProductId, Url): drop http rows whose
--    https twin already exists for the same product, then rewrite the rest.
------------------------------------------------------------------------------
DELETE pi
FROM [dbo].[ProductImage] pi
WHERE pi.[Url] LIKE @Match
  AND EXISTS (SELECT 1 FROM [dbo].[ProductImage] x
              WHERE x.[ProductId] = pi.[ProductId]
                AND x.[Url] = STUFF(pi.[Url], 1, LEN(@OldPrefix), @NewPrefix));
PRINT 'ProductImage duplicate http rows deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[ProductImage]
SET [Url] = STUFF([Url], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [Url] LIKE @Match;
PRINT 'ProductImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 3. TemplateProductImage — same unique-index pattern on (TemplateProductId, Url).
------------------------------------------------------------------------------
DELETE ti
FROM [dbo].[TemplateProductImage] ti
WHERE ti.[Url] LIKE @Match
  AND EXISTS (SELECT 1 FROM [dbo].[TemplateProductImage] x
              WHERE x.[TemplateProductId] = ti.[TemplateProductId]
                AND x.[Url] = STUFF(ti.[Url], 1, LEN(@OldPrefix), @NewPrefix));
PRINT 'TemplateProductImage duplicate http rows deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[TemplateProductImage]
SET [Url] = STUFF([Url], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [Url] LIKE @Match;
PRINT 'TemplateProductImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

------------------------------------------------------------------------------
-- 4. Remaining single-column tables (no unique index on the url column).
------------------------------------------------------------------------------
UPDATE [dbo].[ProductSiteImage]
SET [Url] = STUFF([Url], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [Url] LIKE @Match;
PRINT 'ProductSiteImage rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[ProductVariant]
SET [ImageUrl] = STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [ImageUrl] LIKE @Match;
PRINT 'ProductVariant rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[TemplateProductVariant]
SET [ImageUrl] = STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [ImageUrl] LIKE @Match;
PRINT 'TemplateProductVariant rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[Account]
SET [LogoUrl] = STUFF([LogoUrl], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [LogoUrl] LIKE @Match;
PRINT 'Account rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[Brand]
SET [ImageUrl] = CASE WHEN [ImageUrl] LIKE @Match THEN STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix) ELSE [ImageUrl] END,
    [IconUrl]  = CASE WHEN [IconUrl]  LIKE @Match THEN STUFF([IconUrl],  1, LEN(@OldPrefix), @NewPrefix) ELSE [IconUrl] END
WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
PRINT 'Brand rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[Category]
SET [ImageUrl] = CASE WHEN [ImageUrl] LIKE @Match THEN STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix) ELSE [ImageUrl] END,
    [IconUrl]  = CASE WHEN [IconUrl]  LIKE @Match THEN STUFF([IconUrl],  1, LEN(@OldPrefix), @NewPrefix) ELSE [IconUrl] END
WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
PRINT 'Category rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[GlobalBrand]
SET [ImageUrl] = CASE WHEN [ImageUrl] LIKE @Match THEN STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix) ELSE [ImageUrl] END,
    [IconUrl]  = CASE WHEN [IconUrl]  LIKE @Match THEN STUFF([IconUrl],  1, LEN(@OldPrefix), @NewPrefix) ELSE [IconUrl] END
WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
PRINT 'GlobalBrand rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[GlobalCategory]
SET [ImageUrl] = CASE WHEN [ImageUrl] LIKE @Match THEN STUFF([ImageUrl], 1, LEN(@OldPrefix), @NewPrefix) ELSE [ImageUrl] END,
    [IconUrl]  = CASE WHEN [IconUrl]  LIKE @Match THEN STUFF([IconUrl],  1, LEN(@OldPrefix), @NewPrefix) ELSE [IconUrl] END
WHERE [ImageUrl] LIKE @Match OR [IconUrl] LIKE @Match;
PRINT 'GlobalCategory rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[KioskSettings]
SET [KioskLogoUrl] = STUFF([KioskLogoUrl], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [KioskLogoUrl] LIKE @Match;
PRINT 'KioskSettings rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

UPDATE [dbo].[User]
SET [AvatarUrl] = STUFF([AvatarUrl], 1, LEN(@OldPrefix), @NewPrefix)
WHERE [AvatarUrl] LIKE @Match;
PRINT 'User rows updated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

COMMIT TRANSACTION;
PRINT 'Done.';
