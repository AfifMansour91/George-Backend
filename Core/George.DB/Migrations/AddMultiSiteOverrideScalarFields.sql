-- Migration: MultiSite Phase 2 - per-site scalar field overrides on ProductSiteOverride.
-- Adds Name, ShortDescription, LongDescription, Weight, WeightUnit, Sku, SeoTitle, SeoDescription
-- so name/description/weight/sku/seo can be overridden per branch (full per-site editing of scalars).
-- Idempotent. Run after AddMultiSiteProductOverrides.sql.

IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Name') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Name] nvarchar(300) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ShortDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ShortDescription] nvarchar(2000) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LongDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LongDescription] nvarchar(max) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Weight') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Weight] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'WeightUnit') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [WeightUnit] nvarchar(5) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Sku') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Sku] nvarchar(100) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SeoTitle') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SeoTitle] nvarchar(300) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SeoDescription') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SeoDescription] nvarchar(2000) NULL;
GO
