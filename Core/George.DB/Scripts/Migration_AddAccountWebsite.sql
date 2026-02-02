-- Add Website column to Account table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'Website')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [Website] [nvarchar](500) NULL;
END
GO
