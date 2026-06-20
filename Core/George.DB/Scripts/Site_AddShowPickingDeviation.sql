-- Show picking deviation summary (footer + archive) per site. Default: on.
IF COL_LENGTH(N'dbo.Site', N'ShowPickingDeviation') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowPickingDeviation] BIT NULL;
END
GO

UPDATE [dbo].[Site]
SET [ShowPickingDeviation] = 1
WHERE [ShowPickingDeviation] IS NULL;
GO
