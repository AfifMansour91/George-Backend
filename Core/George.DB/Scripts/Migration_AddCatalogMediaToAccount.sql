-- Migration: Add catalog (global) media to an account by DUPLICATING each catalog media
-- Description: Catalog media = Media not linked to any account (AccountMedia).
--              For each such media, inserts a NEW Media row (copy) and links it to the
--              target account via AccountMedia. The original catalog media rows are unchanged.
--
-- Usage: Set @AccountId to the target account ID below, then execute the script.

USE [George]
GO

-- ========== SET THE TARGET ACCOUNT ID HERE ==========
DECLARE @AccountId INT = 1;
-- ====================================================

-- Validate account exists
IF NOT EXISTS (SELECT 1 FROM [dbo].[Account] WHERE Id = @AccountId)
BEGIN
    RAISERROR('Account with Id %d does not exist.', 16, 1, @AccountId);
    RETURN;
END

-- Table of catalog media to duplicate (no subquery in the INSERT that uses OUTPUT)
DECLARE @CatalogMedia TABLE (
    [Url] NVARCHAR(1000),
    [Name] NVARCHAR(300),
    [TypeId] INT,
    [BusinessTypeId] INT,
    [FileSize] BIGINT,
    [UsageCount] INT
);

INSERT INTO @CatalogMedia ([Url], [Name], [TypeId], [BusinessTypeId], [FileSize], [UsageCount])
SELECT m.[Url], m.[Name], m.[TypeId], m.[BusinessTypeId], m.[FileSize], ISNULL(m.[UsageCount], 0)
FROM [dbo].[Media] m
WHERE m.[IsDeleted] = 0
  AND NOT EXISTS (SELECT 1 FROM [dbo].[AccountMedia] am WHERE am.[MediaId] = m.[Id]);

-- Table to capture new Media.Id for each inserted duplicate
DECLARE @NewMediaIds TABLE (NewId INT);

-- Duplicate each catalog media into a new Media row
INSERT INTO [dbo].[Media] (
    [IsDeleted],
    [CreationTime],
    [UpdatedDate],
    [CreationUserId],
    [UpdateUserId],
    [Url],
    [Name],
    [TypeId],
    [BusinessTypeId],
    [FileSize],
    [UsageCount]
)
OUTPUT inserted.[Id] INTO @NewMediaIds ([NewId])
SELECT
    0,
    sysutcdatetime(),
    NULL,
    NULL,
    NULL,
    c.[Url],
    c.[Name],
    c.[TypeId],
    c.[BusinessTypeId],
    c.[FileSize],
    c.[UsageCount]
FROM @CatalogMedia c;

-- Link the new (duplicated) media to the account
INSERT INTO [dbo].[AccountMedia] ([AccountId], [MediaId], [CreationTime])
SELECT @AccountId, [NewId], sysutcdatetime()
FROM @NewMediaIds;

DECLARE @RowCount INT = (SELECT COUNT(*) FROM @NewMediaIds);
PRINT 'Duplicated ' + CAST(@RowCount AS NVARCHAR(20)) + ' catalog media item(s) and linked them to account ' + CAST(@AccountId AS NVARCHAR(20));
GO
