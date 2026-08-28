-- Sprint 4: per-site promotion webhook URL + signing secret.
-- Spec: `Sprint4/מבצעים.md` - "סנכרון מבצעים לאתר ולקיוסק (Webhook)".
-- Idempotent: each ALTER guarded so the script can be re-run safely.

IF COL_LENGTH(N'dbo.Site', N'PromotionWebhookUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionWebhookUrl NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PromotionWebhookSecret') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionWebhookSecret NVARCHAR(200) NULL;
END
GO
