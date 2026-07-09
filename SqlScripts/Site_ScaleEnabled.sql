-- Per-site opt-in for the connected RS232 scale (live weight in picking).
-- Off by default (NULL/0); only branches with a scale set it to 1.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Site') AND name = N'ScaleEnabled'
)
BEGIN
    ALTER TABLE dbo.Site ADD ScaleEnabled BIT NULL;
END
GO
