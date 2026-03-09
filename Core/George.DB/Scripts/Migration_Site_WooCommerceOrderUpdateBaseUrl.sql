-- Site: optional base URL for updating order status on store side (e.g. oc-storeos: https://.../wp-json/oc-storeos/v1). When set, we PUT to {base}/orders/{id} with status.
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Site')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceOrderUpdateBaseUrl')
    BEGIN
        ALTER TABLE [dbo].[Site] ADD [WooCommerceOrderUpdateBaseUrl] [nvarchar](500) NULL;
        PRINT 'Added WooCommerceOrderUpdateBaseUrl to Site'
    END
END
GO
