-- =============================================
-- Script: Add WooCommerce Integration Fields
-- Description: Adds WooCommerce fields to Site, Category, Product, and ProductVariant tables
-- Date: 2026-01-XX
-- =============================================

USE [George.Dev.V3]
GO

-- =============================================
-- 1. Add WooCommerce fields to Site table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceUrl')
BEGIN
    ALTER TABLE [dbo].[Site]
    ADD [WooCommerceUrl] [nvarchar](500) NULL;
    
    PRINT 'Added WooCommerceUrl column to Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceUrl column already exists in Site table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceKey')
BEGIN
    ALTER TABLE [dbo].[Site]
    ADD [WooCommerceKey] [nvarchar](250) NULL;
    
    PRINT 'Added WooCommerceKey column to Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceKey column already exists in Site table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceSecret')
BEGIN
    ALTER TABLE [dbo].[Site]
    ADD [WooCommerceSecret] [nvarchar](250) NULL;
    
    PRINT 'Added WooCommerceSecret column to Site table';
END
ELSE
BEGIN
    PRINT 'WooCommerceSecret column already exists in Site table';
END
GO

-- =============================================
-- 2. Add WooCommerceId to Category table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Category]') AND name = 'WooCommerceId')
BEGIN
    ALTER TABLE [dbo].[Category]
    ADD [WooCommerceId] [int] NULL;
    
    PRINT 'Added WooCommerceId column to Category table';
END
ELSE
BEGIN
    PRINT 'WooCommerceId column already exists in Category table';
END
GO

-- =============================================
-- 3. Add WooCommerceId to Product table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = 'WooCommerceId')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [WooCommerceId] [int] NULL;
    
    PRINT 'Added WooCommerceId column to Product table';
END
ELSE
BEGIN
    PRINT 'WooCommerceId column already exists in Product table';
END
GO

-- =============================================
-- 4. Add WooCommerceVariationId to ProductVariant table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]') AND name = 'WooCommerceVariationId')
BEGIN
    ALTER TABLE [dbo].[ProductVariant]
    ADD [WooCommerceVariationId] [int] NULL;
    
    PRINT 'Added WooCommerceVariationId column to ProductVariant table';
END
ELSE
BEGIN
    PRINT 'WooCommerceVariationId column already exists in ProductVariant table';
END
GO

-- =============================================
-- 5. Add WooCommerceId to Attribute table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attribute]') AND name = 'WooCommerceId')
BEGIN
    ALTER TABLE [dbo].[Attribute]
    ADD [WooCommerceId] [int] NULL;
    
    PRINT 'Added WooCommerceId column to Attribute table';
END
ELSE
BEGIN
    PRINT 'WooCommerceId column already exists in Attribute table';
END
GO

-- =============================================
-- Verification: Check all columns were added
-- =============================================
PRINT '';
PRINT '=== Verification ===';
PRINT '';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceUrl')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceKey')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'WooCommerceSecret')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Category]') AND name = 'WooCommerceId')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = 'WooCommerceId')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]') AND name = 'WooCommerceVariationId')
    AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Attribute]') AND name = 'WooCommerceId')
BEGIN
    PRINT 'SUCCESS: All WooCommerce fields have been added successfully!';
END
ELSE
BEGIN
    PRINT 'WARNING: Some fields may not have been added. Please review the output above.';
END
GO

PRINT '';
PRINT 'Migration script completed.';
GO

