-- Partner API (/Partner/v1) - external ordering integrations such as the Zano Dagim WhatsApp ordering agent.
-- Adds the per-site columns the Partner API needs. Run once against the George database BEFORE deploying the
-- build that contains PartnerController. Safe to re-run.
--
--   PartnerApiKey        - per-site API key (header X-Api-Key, "pk_" prefix). Generated from
--                          POST /Partner/Admin/GenerateSiteApiKey?siteId=... (JWT). Independent of InternalApiKey
--                          (the WooCommerce plugin key) so each can be rotated/revoked on its own.
--   PartnerWebhookUrl    - optional URL George POSTs order events to (status / payment / delivery changes of
--                          partner-sourced orders). Set via POST /Partner/Admin/Webhook.
--   PartnerWebhookSecret - HMAC-SHA256 secret for the X-Partner-Signature header. Write-only.
--
-- After running: generate a key for the pilot site (Zano Dagim = site 45) and hand it to the vendor.

IF COL_LENGTH(N'dbo.Site', N'PartnerApiKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PartnerApiKey] NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PartnerWebhookUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PartnerWebhookUrl] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PartnerWebhookSecret') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PartnerWebhookSecret] NVARCHAR(200) NULL;
END
GO 

-- Lookup by key on every partner request. Deliberately NOT a filtered index: a filtered index on Site would make
-- every UPDATE of Site fail from a connection with QUOTED_IDENTIFIER OFF (sqlcmd default) - Site is tiny anyway.
IF EXISTS (SELECT 1 FROM sys.indexes  WHERE name = N'IX_Site_PartnerApiKey' AND object_id = OBJECT_ID(N'dbo.Site') AND has_filter = 1)
BEGIN
    DROP INDEX [IX_Site_PartnerApiKey] ON [dbo].[Site];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Site_PartnerApiKey' AND object_id = OBJECT_ID(N'dbo.Site'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Site_PartnerApiKey] ON [dbo].[Site] ([PartnerApiKey]);
END
GO
