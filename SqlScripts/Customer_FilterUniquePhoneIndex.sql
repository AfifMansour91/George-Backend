-- Rebuild IX_Customer_SiteId_NormalizedPhone as a FILTERED unique index so multiple
-- phone-less customers (NormalizedPhone = '') can coexist per site.
-- Required for the customers-screen import feature (email-only contacts) and also
-- fixes a latent bug: creating a second phone-less customer manually crashed on the
-- unfiltered unique index. Lookup code ignores phones shorter than 4 digits, so
-- empty-phone rows are never matched by phone anyway.
-- Idempotent; also bundled inside Import_ZanoDagim_Customers.sql.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'IX_Customer_SiteId_NormalizedPhone'
             AND object_id = OBJECT_ID(N'[dbo].[Customer]')
             AND has_filter = 0)
BEGIN
    DROP INDEX [IX_Customer_SiteId_NormalizedPhone] ON [dbo].[Customer];
    CREATE UNIQUE INDEX [IX_Customer_SiteId_NormalizedPhone]
        ON [dbo].[Customer] ([SiteId], [NormalizedPhone])
        WHERE [NormalizedPhone] <> N'';
END
