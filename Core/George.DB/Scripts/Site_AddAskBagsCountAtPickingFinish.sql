-- Adds per-site flag: prompt for bag count when completing picking (warehouse UI).
-- Run once against the George database. Safe to re-run.
--
-- IMPORTANT: ALTER TABLE and UPDATE must be in separate batches (GO). SQL Server does not
-- allow referencing a newly added column in the same batch as ALTER TABLE ADD.

IF COL_LENGTH(N'dbo.Site', N'AskBagsCountAtPickingFinish') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AskBagsCountAtPickingFinish] BIT NULL;
END
GO

-- New batch: column is now compiled and visible.
UPDATE [dbo].[Site]
SET [AskBagsCountAtPickingFinish] = 1
WHERE [AskBagsCountAtPickingFinish] IS NULL;
GO
