
GO
SET IDENTITY_INSERT [dbo].[Role] ON 
GO
INSERT [dbo].[Role] ([Id], [Name], [IsDeleted]) VALUES (1, N'super_admin', 0)
GO
INSERT [dbo].[Role] ([Id], [Name], [IsDeleted]) VALUES (2, N'account_admin', 0)
GO
INSERT [dbo].[Role] ([Id], [Name], [IsDeleted]) VALUES (3, N'site_admin', 0)
GO
SET IDENTITY_INSERT [dbo].[Role] OFF
GO
SET IDENTITY_INSERT [dbo].[UserStatus] ON 
GO
INSERT [dbo].[UserStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'active', 0)
GO
INSERT [dbo].[UserStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'inactive', 0)
GO
INSERT [dbo].[UserStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'suspended', 0)
GO
SET IDENTITY_INSERT [dbo].[UserStatus] OFF
GO






GO
SET IDENTITY_INSERT [dbo].[AccountStatus] ON 
GO
INSERT [dbo].[AccountStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'Active', 0)
GO
INSERT [dbo].[AccountStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'Inactive', 0)
GO
INSERT [dbo].[AccountStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'Suspended', 0)
GO
SET IDENTITY_INSERT [dbo].[AccountStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[WizardStatus] ON 
GO
INSERT [dbo].[WizardStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'Not Started', 0)
GO
INSERT [dbo].[WizardStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'In Progress', 0)
GO
INSERT [dbo].[WizardStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'Completed', 0)
GO
SET IDENTITY_INSERT [dbo].[WizardStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[WizardType] ON 
GO
INSERT [dbo].[WizardType] ([Id], [Name], [IsDeleted]) VALUES (1, N'all_sites', 0)
GO
INSERT [dbo].[WizardType] ([Id], [Name], [IsDeleted]) VALUES (2, N'per_site', 0)
GO
SET IDENTITY_INSERT [dbo].[WizardType] OFF
GO
SET IDENTITY_INSERT [dbo].[ContentOwner] ON 
GO
INSERT [dbo].[ContentOwner] ([Id], [Name], [IsDeleted]) VALUES (1, N'Company', 0)
GO
INSERT [dbo].[ContentOwner] ([Id], [Name], [IsDeleted]) VALUES (2, N'Client', 0)
GO
SET IDENTITY_INSERT [dbo].[ContentOwner] OFF
GO
SET IDENTITY_INSERT [dbo].[StockManagementType] ON 
GO
INSERT [dbo].[StockManagementType] ([Id], [Name], [IsDeleted]) VALUES (1, N'quantity', 0)
GO
INSERT [dbo].[StockManagementType] ([Id], [Name], [IsDeleted]) VALUES (2, N'status', 0)
GO
SET IDENTITY_INSERT [dbo].[StockManagementType] OFF
GO
SET IDENTITY_INSERT [dbo].[StockStatus] ON 
GO
INSERT [dbo].[StockStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'in_stock', 0)
GO
INSERT [dbo].[StockStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'out_of_stock', 0)
GO
INSERT [dbo].[StockStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'on_backorder', 0)
GO
SET IDENTITY_INSERT [dbo].[StockStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[ShippingClass] ON 
GO
INSERT [dbo].[ShippingClass] ([Id], [Name], [IsDeleted]) VALUES (1, N'default', 0)
GO
INSERT [dbo].[ShippingClass] ([Id], [Name], [IsDeleted]) VALUES (2, N'heavy', 0)
GO
INSERT [dbo].[ShippingClass] ([Id], [Name], [IsDeleted]) VALUES (3, N'fragile', 0)
GO
SET IDENTITY_INSERT [dbo].[ShippingClass] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductStatus] ON 
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'published', 0)
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'draft', 0)
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'archived', 0)
GO
SET IDENTITY_INSERT [dbo].[ProductStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[Visibility] ON 
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (1, N'public', 0)
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (2, N'hidden', 0)
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (3, N'private', 0)
GO
SET IDENTITY_INSERT [dbo].[Visibility] OFF
GO
SET IDENTITY_INSERT [dbo].[SetupType] ON 
GO
INSERT [dbo].[SetupType] ([Id], [Name], [IsDeleted]) VALUES (1, N'standard', 0)
GO
INSERT [dbo].[SetupType] ([Id], [Name], [IsDeleted]) VALUES (2, N'by_unit', 0)
GO
INSERT [dbo].[SetupType] ([Id], [Name], [IsDeleted]) VALUES (3, N'by_weight', 0)
GO
INSERT [dbo].[SetupType] ([Id], [Name], [IsDeleted]) VALUES (4, N'by_unit_and_weight', 0)
GO
SET IDENTITY_INSERT [dbo].[SetupType] OFF
GO
SET IDENTITY_INSERT [dbo].[UnitWeightMode] ON 
GO
INSERT [dbo].[UnitWeightMode] ([Id], [Name], [IsDeleted]) VALUES (1, N'average', 0)
GO
INSERT [dbo].[UnitWeightMode] ([Id], [Name], [IsDeleted]) VALUES (2, N'variable', 0)
GO
INSERT [dbo].[UnitWeightMode] ([Id], [Name], [IsDeleted]) VALUES (3, N'by_variant', 0)
GO
SET IDENTITY_INSERT [dbo].[UnitWeightMode] OFF
GO
SET IDENTITY_INSERT [dbo].[Unit] ON 
GO
INSERT [dbo].[Unit] ([Id], [Name], [IsDeleted]) VALUES (1, N'kg', 0)
GO
INSERT [dbo].[Unit] ([Id], [Name], [IsDeleted]) VALUES (2, N'g', 0)
GO
SET IDENTITY_INSERT [dbo].[Unit] OFF
GO
SET IDENTITY_INSERT [dbo].[MediaType] ON 
GO
INSERT [dbo].[MediaType] ([Id], [Name], [IsDeleted]) VALUES (1, N'image', 0)
GO
INSERT [dbo].[MediaType] ([Id], [Name], [IsDeleted]) VALUES (2, N'video', 0)
GO
INSERT [dbo].[MediaType] ([Id], [Name], [IsDeleted]) VALUES (3, N'document', 0)
GO
SET IDENTITY_INSERT [dbo].[MediaType] OFF
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSAccessKey', N'AKIA5B66OLOHC6N2FFM2', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSBucket', N'teragon', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSKeySecret', N'NMSLE0zarBOwz7PSE01CgVnC1bfMholjcn6m/D30', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'EmailSenderDisplayEmail', N'admin@teragon.com', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'EmailSenderDisplayName', N'Teragon Admin', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'EnvironmentName', N'DEV', N'MUST be "PROD" for production.')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'OtpExpirationInMin', N'15', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'RefreshDataPageSize', N'10', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'RefreshDataWaitTimeInMillisec', N'10', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'RefreshDataWaitTimeLongInMillisec', N'1000', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'StorageExternalBasePath', N'https://teragon.s3.eu-central-1.amazonaws.com/', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'StorageInternalBasePath', NULL, NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'StorageLocalExternalBasePath', N'c:\Teragon', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'StorageLocalInternalBasePath', N'c:\Teragon', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'TempFolder', N'Temp', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'WebAppUrl', N'http://teragon-app.com', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Jwt:Issuer', N'https://meat-admin.local', N'JWT issuer')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Jwt:Audience', N'https://meat-admin.local', N'JWT audience')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Jwt:AccessTokenHours', N'2', N'Access token lifetime (hours)')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Jwt:RefreshTokenDays', N'30', N'Refresh token lifetime (days)')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Ui:DefaultLanguage', N'he-IL', N'Default UI language')
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'Wizard:MaxStep', N'4', N'Wizard step count')
GO






