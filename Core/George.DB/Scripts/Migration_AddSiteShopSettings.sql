-- =============================================
-- Script: Add Shop Settings columns to Site (Sprint 2)
-- Description: Weight tolerance, depreciation, prep time, shipping, print settings
-- =============================================

-- WeightTolerancePercent
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WeightTolerancePercent')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [WeightTolerancePercent] [int] NULL;
    PRINT 'Added WeightTolerancePercent to Site';
END
GO

-- DepreciationEnabled
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'DepreciationEnabled')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [DepreciationEnabled] [bit] NULL;
    PRINT 'Added DepreciationEnabled to Site';
END
GO

-- DepreciationPercentagesJson
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'DepreciationPercentagesJson')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [DepreciationPercentagesJson] [nvarchar](200) NULL;
    PRINT 'Added DepreciationPercentagesJson to Site';
END
GO

-- PrepTimeMinutes
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrepTimeMinutes')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrepTimeMinutes] [int] NULL;
    PRINT 'Added PrepTimeMinutes to Site';
END
GO

-- ShippingCost
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'ShippingCost')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShippingCost] [decimal](18,2) NULL;
    PRINT 'Added ShippingCost to Site';
END
GO

-- FreeShippingAbove
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'FreeShippingAbove')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [FreeShippingAbove] [decimal](18,2) NULL;
    PRINT 'Added FreeShippingAbove to Site';
END
GO

-- AutoPrintEnabled
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'AutoPrintEnabled')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AutoPrintEnabled] [bit] NULL;
    PRINT 'Added AutoPrintEnabled to Site';
END
GO

-- PrintNewOrderImmediate
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrintNewOrderImmediate')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintNewOrderImmediate] [bit] NULL;
    PRINT 'Added PrintNewOrderImmediate to Site';
END
GO

-- PrintMovedToTreatment
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrintMovedToTreatment')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintMovedToTreatment] [bit] NULL;
    PRINT 'Added PrintMovedToTreatment to Site';
END
GO

-- PrintFutureImmediate
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrintFutureImmediate')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintFutureImmediate] [bit] NULL;
    PRINT 'Added PrintFutureImmediate to Site';
END
GO

-- PrintFutureAtTimeEnabled
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrintFutureAtTimeEnabled')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintFutureAtTimeEnabled] [bit] NULL;
    PRINT 'Added PrintFutureAtTimeEnabled to Site';
END
GO

-- PrintFutureAtTime
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'PrintFutureAtTime')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintFutureAtTime] [nvarchar](10) NULL;
    PRINT 'Added PrintFutureAtTime to Site';
END
GO

PRINT 'Migration_AddSiteShopSettings completed.';
GO
