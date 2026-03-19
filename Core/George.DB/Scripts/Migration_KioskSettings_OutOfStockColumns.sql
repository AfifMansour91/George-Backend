-- Add kiosk out-of-stock behavior columns.
-- Run once on existing databases.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'ShowOutOfStockProducts')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD ShowOutOfStockProducts BIT NOT NULL CONSTRAINT DF_KioskSettings_ShowOutOfStockProducts DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'ShowOutOfStockAtBottom')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD ShowOutOfStockAtBottom BIT NOT NULL CONSTRAINT DF_KioskSettings_ShowOutOfStockAtBottom DEFAULT 0;
END
GO
