-- Promotion priority for evaluation order (lower = first). Idempotent.
IF COL_LENGTH('dbo.Promotion', 'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion
        ADD Priority INT NOT NULL CONSTRAINT DF_Promotion_Priority DEFAULT (10);
END
GO
