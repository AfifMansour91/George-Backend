-- Migration: Add IsraelCityPickerEnabled to Site (searchable city list for manual/phone orders).
-- Default TRUE for existing rows. Run in SSMS or sqlcmd against your George DB.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = N'IsraelCityPickerEnabled'
)
BEGIN
    ALTER TABLE [dbo].[Site] ADD [IsraelCityPickerEnabled] bit NULL;
END
GO

UPDATE [dbo].[Site]
SET [IsraelCityPickerEnabled] = 1
WHERE [IsraelCityPickerEnabled] IS NULL;
GO
