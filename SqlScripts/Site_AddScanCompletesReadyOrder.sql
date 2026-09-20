-- Site.ScanCompletesReadyOrder: scanning the voucher barcode of a READY order completes (hands over) the order
-- instead of opening its window. NULL / 0 = open the window (the behaviour so far). Idempotent.
IF COL_LENGTH(N'dbo.Site', N'ScanCompletesReadyOrder') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD ScanCompletesReadyOrder BIT NULL;
    PRINT 'Site.ScanCompletesReadyOrder added';
END
ELSE
    PRINT 'Site.ScanCompletesReadyOrder already exists';
GO
