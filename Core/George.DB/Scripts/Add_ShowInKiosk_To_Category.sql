-- Add ShowInKiosk to Category (kiosk visibility; default 1 = show).
-- Run once per environment. Safe to run: adds column only if missing.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Category') AND name = N'ShowInKiosk'
)
BEGIN
    ALTER TABLE dbo.Category
    ADD ShowInKiosk BIT NOT NULL CONSTRAINT DF_Category_ShowInKiosk DEFAULT 1;
END
GO
