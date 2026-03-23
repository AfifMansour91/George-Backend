-- Widen ClientSource so enriched client strings (app | user id | email | ip) fit.
-- Safe to run multiple times: only alters when column is narrower than NVARCHAR(256).
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.PrintJob')
      AND name = N'ClientSource'
      AND max_length < 512 -- nvarchar(256) => max_length 512 in sys.columns
)
    ALTER TABLE dbo.PrintJob ALTER COLUMN [ClientSource] NVARCHAR(256) NULL;
