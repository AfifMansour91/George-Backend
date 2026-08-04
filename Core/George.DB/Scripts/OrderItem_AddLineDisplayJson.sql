-- Adds OrderItem.LineDisplayJson: typed display snapshot (JSON) written at line creation
-- (sale kind + clean size/cutting names + numeric weights). Surfaces render from it when
-- Site.UseStructuredOrderLineDisplay is on; NULL (legacy lines) keeps the label heuristics.
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.OrderItem', N'LineDisplayJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem] ADD [LineDisplayJson] NVARCHAR(MAX) NULL;
END
GO
