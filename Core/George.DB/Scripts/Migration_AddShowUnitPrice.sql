-- Migration: Add ShowUnitPrice to WeightConfig
-- Date: 2026-01-24
-- Description: Adds ShowUnitPrice column to WeightConfig table to support displaying unit price instead of price per kg

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[WeightConfig]') 
    AND name = 'ShowUnitPrice'
)
BEGIN
    ALTER TABLE [dbo].[WeightConfig]
    ADD [ShowUnitPrice] [bit] NULL;
    
    PRINT 'Added ShowUnitPrice column to WeightConfig table';
END
ELSE
BEGIN
    PRINT 'ShowUnitPrice column already exists in WeightConfig table';
END
GO
