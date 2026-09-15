-- Bundles (מארזים): per-site OC Bundles API key (X-OC-Bundles-Key, write-only in the API)
-- and the "free swap" permission for pickers/managers.
-- Spec: BUNDLES_SYNC_SPEC.md §1 / §3.4. Idempotent.

IF COL_LENGTH(N'dbo.Site', N'BundlesApiKey') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD BundlesApiKey NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'BundleAllowFreeSwap') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD BundleAllowFreeSwap BIT NULL;
END
GO
