-- Migration: MultiSite Phase 2 — per-site merchandising overrides on ProductSiteOverride.
-- Adds CostPrice, IsKosher, StatusId, VisibilityId, Slug, ShippingClassId, SupplierId and the storefront
-- Label* fields so every product field a branch (selected-site) edit touches follows the same model as
-- Price: a canonical all-sites value + an optional per-site override (null = inherit canonical).
-- Idempotent. Run after AddMultiSiteOverrideScalarFields.sql.

IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'CostPrice') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [CostPrice] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsKosher') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsKosher] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StatusId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StatusId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'VisibilityId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [VisibilityId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Slug') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Slug] nvarchar(200) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ShippingClassId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ShippingClassId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SupplierId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SupplierId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelFrozen') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelFrozen] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelGlutenFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelGlutenFree] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNotKosher') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNotKosher] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelKosherForPassover') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelKosherForPassover] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelKosherForPassoverEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelKosherForPassoverEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNew') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNew] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNewEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNewEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelBestseller') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelBestseller] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelLowAvailability') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelLowAvailability] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelReadyToCook') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelReadyToCook] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelNatural') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelNatural] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelSugarFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelSugarFree] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LabelLactoseFree') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LabelLactoseFree] bit NULL;
GO
