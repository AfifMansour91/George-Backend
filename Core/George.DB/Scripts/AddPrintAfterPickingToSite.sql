-- Migration: Add PrintAfterPicking to Site table
-- When true, print voucher automatically when picking is completed (with weights and final price).
-- Run against your database (SQL Server).

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = N'Site'
      AND c.name = N'PrintAfterPicking'
)
BEGIN
    ALTER TABLE [Site]
    ADD [PrintAfterPicking] BIT NULL;
END
GO
