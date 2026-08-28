-- Adds Site.UseStructuredOrderLineDisplay: feature flag - render order-line attributes from the
-- typed OrderItem.LineDisplayJson snapshot (no Hebrew-label parsing) on lines that have one;
-- lines without a snapshot always use the legacy heuristics.
-- NULL/0 = legacy rendering everywhere (default, current behavior).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'UseStructuredOrderLineDisplay') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [UseStructuredOrderLineDisplay] BIT NULL;
END
GO
