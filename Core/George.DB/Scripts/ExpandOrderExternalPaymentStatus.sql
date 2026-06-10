-- Optional: allow longer gateway / saved-card status messages (default migration used 100).
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.[Order]')
      AND c.name = N'ExternalPaymentStatus'
      AND t.name = N'nvarchar'
      AND c.max_length < 1000
)
BEGIN
    ALTER TABLE dbo.[Order] ALTER COLUMN ExternalPaymentStatus NVARCHAR(500) NULL;
END
GO
