-- Adds Site.InvoicePromotionDisplayMode - Cardcom invoice layout for promotion lines (הקצב בעיר, Cardcom support 2026-10-14):
--   'discount_total' - catalog price on every line + one "הנחה" line before the total (today's default)
--   'net_price'      - the deal's ₪/unit in the product line with the promotion name, no discount line
--                      (= the old InvoicePromotionAsLinePrice = 1)
--   'line_discount'  - catalog price on the product line + a negative "הנחה - {מבצע} ({מוצר})" line right under each
--                      discounted product (Cardcom's v11 Products schema has no per-line discount field).
-- Backfills the mode from the legacy bool. Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'InvoicePromotionDisplayMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [InvoicePromotionDisplayMode] NVARCHAR(20) NULL;
    PRINT 'Added InvoicePromotionDisplayMode to Site';
END
GO
UPDATE [dbo].[Site]
SET [InvoicePromotionDisplayMode] = CASE WHEN [InvoicePromotionAsLinePrice] = 1 THEN N'net_price' ELSE N'discount_total' END
WHERE [InvoicePromotionDisplayMode] IS NULL;
PRINT CONCAT('InvoicePromotionDisplayMode backfilled: ', @@ROWCOUNT, ' sites');
GO
