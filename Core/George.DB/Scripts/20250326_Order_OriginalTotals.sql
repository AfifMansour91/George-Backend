-- Snapshot columns: merchandise + grand total as first saved (before picking adjustments).
-- Run once against the George database.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'OriginalSubTotal')
    ALTER TABLE dbo.[Order] ADD OriginalSubTotal DECIMAL(18, 2) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'OriginalTotal')
    ALTER TABLE dbo.[Order] ADD OriginalTotal DECIMAL(18, 2) NULL;

-- Optional backfill (may be wrong for orders already adjusted by picking):
-- UPDATE dbo.[Order]
-- SET OriginalSubTotal = SubTotal, OriginalTotal = Total
-- WHERE OriginalSubTotal IS NULL AND OriginalTotal IS NULL;
