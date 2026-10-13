-- Adds Site.InvoicePromotionAsLinePrice: Cardcom invoice layout for promotion lines.
-- NULL/0 = current behavior: every line at the catalog price per unit, and one "הנחה" (ADJUSTMENT) line
--          reconciles the document with the charged total.
-- 1      = each discounted line is printed at its net price per unit (line net ÷ quantity) with the
--          promotion name appended to the description, so no separate discount line is needed
--          (requested by הקצב בעיר, 2026-10-13). Manual / whole-cart discounts still reconcile via ADJUSTMENT.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'InvoicePromotionAsLinePrice') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [InvoicePromotionAsLinePrice] BIT NULL;
END
GO
