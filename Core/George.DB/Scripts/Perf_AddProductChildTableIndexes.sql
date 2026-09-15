-- Perf indexes backed by 6 months of missing-index DMV data (SQL start 2026-03-04, checked 2026-09-01):
--   ProductVariant(ProductId, IsDeleted): demanded by ~300k query executions; the table (23k rows) had
--     ZERO nonclustered indexes, so every product load/sync scanned it.
--   ProductOption(ProductId, IsDeleted): demanded by ~225k executions; same access pattern.
-- Key-only on purpose - the DMV's fat INCLUDE suggestions cover specific queries but bloat writes;
-- the seek + key lookup is the win here.
-- Run once against George.Prod. Safe to re-run. Instant on tables this size.

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductVariant_Product_IsDeleted')
    CREATE INDEX IX_ProductVariant_Product_IsDeleted
        ON ProductVariant (ProductId, IsDeleted);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductOption_Product_IsDeleted')
    CREATE INDEX IX_ProductOption_Product_IsDeleted
        ON ProductOption (ProductId, IsDeleted);
