-- Adds Site.ShowCustomerProfileNoteInPrints: when true, order printouts (voucher/A4) include the
-- permanent customer note (הערה קבועה, Customer.Notes) in the order-notes line.
-- NULL/0 = not printed (default, off).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ShowCustomerProfileNoteInPrints') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowCustomerProfileNoteInPrints] BIT NULL;
END
GO
