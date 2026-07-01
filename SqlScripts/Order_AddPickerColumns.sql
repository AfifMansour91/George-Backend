-- Order picker (מלקט): who picked the order, recorded when picking is saved.
-- PickerUserId references the staff User; PickerName is denormalized from User.FullName
-- at picking time so the archive list can show it without a join.
-- Idempotent: each ALTER is guarded by a COL_LENGTH check so the script can be re-run safely.

IF COL_LENGTH(N'dbo.[Order]', N'PickerUserId') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD PickerUserId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PickerName') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD PickerName NVARCHAR(200) NULL;
END
GO
