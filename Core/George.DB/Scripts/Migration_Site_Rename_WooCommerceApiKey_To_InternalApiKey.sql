-- Rename Site.WooCommerceApiKey to InternalApiKey (if you ran the old migration that added WooCommerceApiKey).
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceApiKey')
BEGIN
    EXEC sp_rename 'dbo.Site.WooCommerceApiKey', 'InternalApiKey', 'COLUMN';
    PRINT 'Renamed Site.WooCommerceApiKey to InternalApiKey'
END
GO
