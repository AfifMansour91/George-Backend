-- Scale barcode payload interpretation for picking scan (13-digit prefix 2 labels).
-- Values: auto | weight | price (default auto = detect from payload).

IF COL_LENGTH(N'dbo.Site', N'ScaleBarcodeEmbedMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ScaleBarcodeEmbedMode] NVARCHAR(16) NULL;
END
GO

UPDATE [dbo].[Site]
SET [ScaleBarcodeEmbedMode] = N'auto'
WHERE [ScaleBarcodeEmbedMode] IS NULL OR LTRIM(RTRIM([ScaleBarcodeEmbedMode])) = N'';
GO
