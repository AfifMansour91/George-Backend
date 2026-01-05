-- =============================================
-- Script: Remove WooCommerce Integration Fields (Rollback)
-- Description: Removes WooCommerce fields from Site, Category, Product, and ProductVariant tables
-- Date: 2026-01-XX
-- WARNING: This will permanently delete data in these columns!
-- =============================================

USE [George.Dev.V3]
GO

-- =============================================
-- 1. Remove WooCommerce fields from Site table
-- =============================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceUrl')
BEGIN
    ALTER TABLE [dbo].[Site]
    DROP COLUMN [WooCommerceUrl];
    
    PRINT 'Removed WooCommerceUrl column from Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceUrl column does not exist in Site table';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceKey')
BEGIN
    ALTER TABLE [dbo].[Site]
    DROP COLUMN [WooCommerceKey];
    
    PRINT 'Removed WooCommerceKey column from Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceKey column does not exist in Site table';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceSecret')
BEGIN
    ALTER TABLE [dbo].[Site]
    DROP COLUMN [WooCommerceSecret];
    
    PRINT 'Removed WooCommerceSecret column from Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceSecret column does not exist in Site table';
END
GO

-- =============================================
-- 2. Remove WooCommerceId from Category table
-- =============================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Category]') AND name = 'WooCommerceId')
BEGIN
    ALTER TABLE [dbo].[Category]
    DROP COLUMN [WooCommerceId];
    
    PRINT 'Removed WooCommerceId column from Category table';
END
ELSE
BEGIN
    PRINT 'WooCommerceId column does not exist in Category table';
END
GO

-- =============================================
-- 3. Remove WooCommerceId from Product table
-- =============================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = 'WooCommerceId')
BEGIN
    ALTER TABLE [dbo].[Product]
    DROP COLUMN [WooCommerceId];
    
    PRINT 'Removed WooCommerceId column from Product table';
END
ELSE
BEGIN
    PRINT 'WooCommerceId column does not exist in Product table';
END
GO

-- =============================================
-- 4. Remove WooCommerceVariationId from ProductVariant table
-- =============================================
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]') AND name = 'WooCommerceVariationId')
BEGIN
    ALTER TABLE [dbo].[ProductVariant]
    DROP COLUMN [WooCommerceVariationId];
    
    PRINT 'Removed WooCommerceVariationId column from ProductVariant table';
END
ELSE
BEGIN
    PRINT 'WooCommerceVariationId column does not exist in ProductVariant table';
END
GO

-- =============================================
-- Verification: Check all columns were removed
-- =============================================
PRINT '';
PRINT '=== Verification ===';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceUrl')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceKey')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceSecret')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Category]') AND name = 'WooCommerceId')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = 'WooCommerceId')
    AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]') AND name = 'WooCommerceVariationId')
BEGIN
    PRINT 'SUCCESS: All WooCommerce fields have been removed successfully!';
END
ELSE
BEGIN
    PRINT 'WARNING: Some fields may still exist. Please review the output above.';
END
GO

PRINT '';
PRINT 'Rollback script completed.';
GO

