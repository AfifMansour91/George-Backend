-- Adds per-site fast-picking settings:
--   FastPickingScan            - fast scan mode: no confirm dialog on scan + auto-finish when all items picked (default OFF)
--   ShowPickingExceptionsPopup - show the deviation (חריגה) popup when a scan exceeds tolerance (default ON)
-- Run once against the George database. Safe to re-run.
--
-- IMPORTANT: ALTER TABLE and UPDATE must be in separate batches (GO). SQL Server does not
-- allow referencing a newly added column in the same batch as ALTER TABLE ADD.

IF COL_LENGTH(N'dbo.Site', N'FastPickingScan') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [FastPickingScan] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'ShowPickingExceptionsPopup') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowPickingExceptionsPopup] BIT NULL;
END
GO

-- New batch: columns are now compiled and visible.
UPDATE [dbo].[Site]
SET [ShowPickingExceptionsPopup] = 1
WHERE [ShowPickingExceptionsPopup] IS NULL;
GO
