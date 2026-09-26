-- Delivery-fee shortcuts for the new-order screen (PEPE 24/9, a stand-in until a real delivery module):
--   Site.ShippingCostPresets  - comma separated quick choices ("15,30,35"); the fee popup shows one button
--                               per value plus "+" for a free amount.
--   SiteCityShippingCost      - the fee last chosen for a city becomes that city's proposed fee for every
--                               later order (any customer). One row per (site, city).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ShippingCostPresets') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShippingCostPresets] NVARCHAR(200) NULL;
    PRINT 'Added ShippingCostPresets to Site';
END
GO

IF OBJECT_ID(N'dbo.SiteCityShippingCost', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SiteCityShippingCost]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SiteCityShippingCost] PRIMARY KEY,
        [SiteId] INT NOT NULL,
        [City] NVARCHAR(120) NOT NULL,
        [Cost] DECIMAL(18, 2) NOT NULL,
        [UpdatedDate] DATETIME2(7) NOT NULL,
        CONSTRAINT [FK_SiteCityShippingCost_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site]([Id])
    );
    CREATE UNIQUE INDEX [UX_SiteCityShippingCost_Site_City] ON [dbo].[SiteCityShippingCost]([SiteId], [City]);
    PRINT 'Created SiteCityShippingCost';
END
GO
