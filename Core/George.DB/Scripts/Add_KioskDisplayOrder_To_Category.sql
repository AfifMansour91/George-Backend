-- Add KioskDisplayOrder to Category (display order in kiosk sidebar; NULL = use default).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Category') AND name = N'KioskDisplayOrder'
)
BEGIN
    ALTER TABLE dbo.Category
    ADD KioskDisplayOrder INT NULL;
END
GO
