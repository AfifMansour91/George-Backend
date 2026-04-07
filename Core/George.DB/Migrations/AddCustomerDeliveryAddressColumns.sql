-- Migration: add structured delivery address columns on Customer (CRM).
-- Mirrors Order delivery fields; keep DefaultAddress + City for legacy / combined line.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Customer]') AND name = N'DeliveryStreet')
    ALTER TABLE [dbo].[Customer] ADD [DeliveryStreet] nvarchar(400) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Customer]') AND name = N'DeliveryApartment')
    ALTER TABLE [dbo].[Customer] ADD [DeliveryApartment] nvarchar(64) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Customer]') AND name = N'DeliveryFloor')
    ALTER TABLE [dbo].[Customer] ADD [DeliveryFloor] nvarchar(32) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Customer]') AND name = N'DeliveryEntranceCode')
    ALTER TABLE [dbo].[Customer] ADD [DeliveryEntranceCode] nvarchar(64) NULL;
GO
