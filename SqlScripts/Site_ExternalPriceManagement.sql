-- Per-site opt-in for externally managed prices (POS updates prices directly on the Woo store).
-- When 1: George never writes price fields on Woo product/variation UPDATES, and the daily
-- price-pull job imports Woo prices back into George for this site. Off by default (NULL/0).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Site') AND name = N'ExternalPriceManagement'
)
BEGIN
    ALTER TABLE dbo.Site ADD ExternalPriceManagement BIT NULL;
END
GO
