-- Run on George DB before deploying API/frontend that send trigger + client source for print jobs.
IF COL_LENGTH('dbo.PrintJob', 'Trigger') IS NULL
    ALTER TABLE dbo.PrintJob ADD [Trigger] NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.PrintJob', 'ClientSource') IS NULL
    ALTER TABLE dbo.PrintJob ADD [ClientSource] NVARCHAR(80) NULL;

-- Backfill existing rows for easier diagnostics.
UPDATE dbo.PrintJob
SET [Trigger] =
    CASE
        WHEN [JobType] LIKE 'VoucherAuto:%' THEN REPLACE([JobType], 'VoucherAuto:', '')
        ELSE 'Manual'
    END
WHERE [Trigger] IS NULL OR LTRIM(RTRIM([Trigger])) = '';

UPDATE dbo.PrintJob
SET [ClientSource] = 'Unknown'
WHERE [ClientSource] IS NULL OR LTRIM(RTRIM([ClientSource])) = '';

-- Optional hardening (run only after clients are upgraded):
-- ALTER TABLE dbo.PrintJob ALTER COLUMN [Trigger] NVARCHAR(80) NOT NULL;
-- ALTER TABLE dbo.PrintJob ALTER COLUMN [ClientSource] NVARCHAR(80) NOT NULL;
