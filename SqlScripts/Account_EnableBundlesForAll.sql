-- Bundles (מארזים): turn the module ON for every existing account and make ON the default for
-- accounts created from now on (new accounts also default to ON in the API/UI, this is the DB-level
-- safety net). Idempotent - safe to re-run.
--
-- Run AFTER Bundles_Prod_Install.sql (needs dbo.Account.BundlesEnabled).

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Account', N'BundlesEnabled') IS NULL
BEGIN
    RAISERROR (N'dbo.Account.BundlesEnabled is missing - run Bundles_Prod_Install.sql first.', 16, 1);
    RETURN;
END
GO

-- 1. Default for new rows: 1 instead of 0.
IF EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Account')
      AND c.name = N'BundlesEnabled'
      AND dc.definition <> N'((1))'
)
BEGIN
    DECLARE @dfName sysname;
    SELECT @dfName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Account') AND c.name = N'BundlesEnabled';

    EXEC (N'ALTER TABLE dbo.Account DROP CONSTRAINT [' + @dfName + N']');
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Account') AND c.name = N'BundlesEnabled'
)
BEGIN
    ALTER TABLE dbo.Account ADD CONSTRAINT DF_Account_BundlesEnabled DEFAULT (1) FOR BundlesEnabled;
END
GO

-- 2. Existing accounts: enable the module everywhere.
UPDATE dbo.Account
SET BundlesEnabled = 1
WHERE BundlesEnabled = 0;

PRINT N'Bundles enabled for all accounts (' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' updated); default for new accounts is now ON.';
GO
