-- Adds Site.VoucherPrintA4: when true, order printouts (manual + auto-print) use a full A4 page layout
-- instead of the thermal voucher (בון). Auto-print jobs are delivered to the PrintAgent as an A4 PDF
-- (existing agents print PDFs via Sumatra - no agent update needed); manual prints use the browser A4 template.
-- NULL/0 = thermal voucher (current behavior).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'VoucherPrintA4') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherPrintA4] BIT NULL;
END
GO
