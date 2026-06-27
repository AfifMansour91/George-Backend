-- Customer KPI refresh: per-site setting — whether customer statistics include
-- orders that haven't been completed yet. Default 1 (true) = preserve current behavior.
-- Idempotent: guarded by COL_LENGTH so the script can be re-run safely.

IF COL_LENGTH(N'dbo.Site', N'IncludeIncompleteOrdersInStats') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        IncludeIncompleteOrdersInStats BIT NULL
            CONSTRAINT DF_Site_IncludeIncompleteOrdersInStats DEFAULT (1);
END
GO
