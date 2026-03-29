
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
SET IDENTITY_INSERT [dbo].[User] ON 
GO
INSERT [dbo].[User] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [RoleId], [AccountId], [StatusId], [FirstName], [LastName], [Email], [IsEmailVerified], [Password], [Otp], [LastLoginDate], [LockoutFailCount], [LockoutExpiration], [RefreshToken], [RefreshTokenExpiration], [Phone], [AvatarUrl], [Notes], [OtpExpiration]) VALUES (1, 0, N'50271d94-dfa5-4a0a-ac29-06bac58f234d', CAST(N'2026-01-04T12:10:30.0000000' AS DateTime2), CAST(N'2026-01-05T20:30:39.0000000' AS DateTime2), NULL, NULL, 1, NULL, 1, N'M', N'Mansour', N'M.mansour@gmail.com', 1, N'12qwaszx', N'111111', CAST(N'2026-01-10T07:12:31.0000000' AS DateTime2), 0, NULL, N'93b0a9f1a4cd417bb703b02ff523980b', CAST(N'2031-09-24T15:12:31.0000000' AS DateTime2), N'0545874251', N'http://qa-api.M-dev.com/files/DEV/Temp/1cc46f2905364eb5ae9739d2fc8a88e9.jpg', NULL, NULL)
GO
SET IDENTITY_INSERT [dbo].[User] OFF
GO







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
INSERT [dbo].[WizardType] ([Id], [Name], [IsDeleted]) VALUES (3, N'none', 0)
GO
SET IDENTITY_INSERT [dbo].[WizardType] OFF
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
SET IDENTITY_INSERT [dbo].[ContentOwner] ON 
GO
INSERT [dbo].[ContentOwner] ([Id], [Name], [IsDeleted]) VALUES (1, N'Company', 0)
GO
INSERT [dbo].[ContentOwner] ([Id], [Name], [IsDeleted]) VALUES (2, N'Client', 0)
GO
SET IDENTITY_INSERT [dbo].[ContentOwner] OFF
GO
SET IDENTITY_INSERT [dbo].[BusinessType] ON 
GO
INSERT [dbo].[BusinessType] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [Icon]) VALUES (1, 0, N'2142d2db-7dc7-4147-8e5a-b08cf191dacf', CAST(N'2026-01-05T23:47:55.0000000' AS DateTime2), NULL, NULL, NULL, N'דגים', N'', N'Fish')
GO
INSERT [dbo].[BusinessType] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [Icon]) VALUES (2, 0, N'e0c0436c-d024-401c-95d0-82aabf844a3f', CAST(N'2026-01-05T23:48:04.0000000' AS DateTime2), NULL, NULL, NULL, N'פירות וירקות', N'', N'Apple')
GO
INSERT [dbo].[BusinessType] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [Icon]) VALUES (3, 0, N'e80a6495-7031-4774-afab-5f415fd9c53a', CAST(N'2026-01-05T23:48:11.0000000' AS DateTime2), NULL, NULL, NULL, N'בשר', N'', N'Beef')
GO
SET IDENTITY_INSERT [dbo].[BusinessType] OFF
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
SET IDENTITY_INSERT [dbo].[Media] ON 
GO
INSERT [dbo].[Media] ([Id], [IsDeleted], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Url], [Name], [TypeId], [BusinessTypeId], [FileSize], [UsageCount]) VALUES (1, 1, CAST(N'2026-01-05T12:35:05.0000000' AS DateTime2), CAST(N'2026-01-05T20:31:00.0000000' AS DateTime2), 1, NULL, N'http://qa-api.M-dev.com/files/DEV/Temp/65fd92c7e1cc416b940d3d2adca0f3a9.jpg', N'7a8fcf95f_--.jpg', 1, NULL, 46589, 0)
GO
SET IDENTITY_INSERT [dbo].[Media] OFF
GO
SET IDENTITY_INSERT [dbo].[Visibility] ON 
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (1, N'active', 0)
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (2, N'hidden', 0)
GO
INSERT [dbo].[Visibility] ([Id], [Name], [IsDeleted]) VALUES (3, N'outOfStock', 0)
GO
SET IDENTITY_INSERT [dbo].[Visibility] OFF
GO
SET IDENTITY_INSERT [dbo].[Unit] ON 
GO
INSERT [dbo].[Unit] ([Id], [Name], [IsDeleted]) VALUES (1, N'kg', 0)
GO
INSERT [dbo].[Unit] ([Id], [Name], [IsDeleted]) VALUES (2, N'g', 0)
GO
SET IDENTITY_INSERT [dbo].[Unit] OFF
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
SET IDENTITY_INSERT [dbo].[WeightConfig] ON 
GO
INSERT [dbo].[WeightConfig] ([Id], [IsDeleted], [UnitId], [StartWeight], [Step], [FixedWeightPerUnit], [UnitWeight], [UnitWeightModeId], [WeightOptions], [WeightByVariant], [ShowPricePer100g], [ShowUnitPrice]) VALUES (1, 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
GO
SET IDENTITY_INSERT [dbo].[WeightConfig] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductStatus] ON 
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (1, N'active', 0)
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (2, N'outOfStock', 0)
GO
INSERT [dbo].[ProductStatus] ([Id], [Name], [IsDeleted]) VALUES (3, N'hidden', 0)
GO
SET IDENTITY_INSERT [dbo].[ProductStatus] OFF
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
SET IDENTITY_INSERT [dbo].[StockManagementType] ON 
GO
INSERT [dbo].[StockManagementType] ([Id], [Name], [IsDeleted]) VALUES (1, N'quantity', 0)
GO
INSERT [dbo].[StockManagementType] ([Id], [Name], [IsDeleted]) VALUES (2, N'status', 0)
GO
INSERT [dbo].[StockManagementType] ([Id], [Name], [IsDeleted]) VALUES (3, N'variation', 0)
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
SET IDENTITY_INSERT [dbo].[TemplateProduct] ON 
GO
INSERT [dbo].[TemplateProduct] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [TemplateId], [Name], [ShortDescription], [LongDescription], [Price], [SalePrice], [SalePriceStartDate], [SalePriceEndDate], [CostPrice], [Sku], [StockManagementTypeId], [StockQuantity], [StockStatusId], [Weight], [ShippingClassId], [StatusId], [VisibilityId], [BrandId], [SupplierId], [IsKosher], [IsWeighted], [SetupTypeId], [WeightConfigId], [SeoTitle], [SeoDescription], [SourceProductId]) VALUES (1, 0, N'f0a56c96-d978-42e4-900e-5b38b721d6db', CAST(N'2026-01-08T11:19:15.0000000' AS DateTime2), NULL, 1, NULL, N'tpl_1767871154812', N'יעיכיעכיעכיעכ', N'', N'', CAST(0.00 AS Decimal(18, 2)), NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, CAST(0.0000 AS Decimal(18, 4)), NULL, 1, NULL, NULL, NULL, 1, 0, 1, 1, NULL, NULL, NULL)
GO
SET IDENTITY_INSERT [dbo].[TemplateProduct] OFF
GO
SET IDENTITY_INSERT [dbo].[TemplateAttribute] ON 
GO
INSERT [dbo].[TemplateAttribute] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name]) VALUES (1, 0, N'763ec595-7b28-4e0a-b1a1-ad3268680c6f', CAST(N'2026-01-05T23:47:41.0000000' AS DateTime2), NULL, 1, NULL, N'צורת חיתוך')
GO
INSERT [dbo].[TemplateAttribute] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name]) VALUES (2, 0, N'253e6d2a-72e0-4f77-b41f-b0fc17ff78f1', CAST(N'2026-01-09T09:07:31.0000000' AS DateTime2), CAST(N'2026-01-09T09:07:38.0000000' AS DateTime2), 1, 1, N'גודל')
GO
SET IDENTITY_INSERT [dbo].[TemplateAttribute] OFF
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'בינוני')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'דק')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'דק דק')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'חצוי נקי')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'חצי')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'חצי יחידה')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'חצי פרפר')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'יחידה')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'לאורך')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'ללא עור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'ללא תיבול')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'מפורק')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'נתח שלם מתובל לתנור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'סטייקים')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'סטייקים דקים')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'ספר')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי 2 אצבעות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי 3 אצבעות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי אצבע')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי אצבע (רגיל)')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי שלוש אצבעות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עובי שתי אצבעות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עם עור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'עם תיבול')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פילה')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פילה ללא עור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פרוס')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פרוסות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פרוסות ללא עור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'פרפר שלם')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'קוביות')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'רגיל')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'שיפודים')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'שלם')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'שלם בלי ראש')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'שלם ללא עור')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (1, N'שלם נקי')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (2, N'בינוני')
GO
INSERT [dbo].[TemplateAttributeValue] ([TemplateAttributeId], [Value]) VALUES (2, N'קטן')
GO
SET IDENTITY_INSERT [dbo].[GlobalCategory] ON 
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (1, 0, N'3d41852b-aad9-4eaa-81d3-95592be99f48', CAST(N'2026-01-05T23:48:33.0000000' AS DateTime2), NULL, 1, NULL, N'בקר', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (2, 0, N'018b5806-4039-4114-a481-f16cf1165397', CAST(N'2026-01-05T23:48:43.0000000' AS DateTime2), NULL, 1, NULL, N'על האש', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (3, 0, N'f789759c-9683-4c3d-a285-a21ee15bafbf', CAST(N'2026-01-05T23:48:52.0000000' AS DateTime2), NULL, 1, NULL, N'קדירה ובישול', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (4, 0, N'1adf6864-3810-4156-bb14-92069b407761', CAST(N'2026-01-05T23:49:00.0000000' AS DateTime2), NULL, 1, NULL, N'טלה', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (5, 0, N'08c981c4-f34e-4ef2-ba75-468e871b5d4a', CAST(N'2026-01-05T23:49:09.0000000' AS DateTime2), NULL, 1, NULL, N'עוף, הודו ואווז', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (6, 0, N'a6261a10-9c7e-4d2b-9f85-a22e7c55a9d9', CAST(N'2026-01-05T23:49:15.0000000' AS DateTime2), NULL, 1, NULL, N'לתנור', N'', NULL, 0, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (7, 0, N'f2100cb6-47bb-4a22-9e41-5478881cb1d3', CAST(N'2026-01-05T23:49:25.0000000' AS DateTime2), CAST(N'2026-01-05T23:49:32.0000000' AS DateTime2), 1, 1, N'טחונים', N'', 1, NULL, NULL)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (8, 0, N'83c5a9ff-908a-4291-aa07-3a12536a1cc9', CAST(N'2026-01-05T23:49:42.0000000' AS DateTime2), NULL, 1, NULL, N'סטייקים', N'', 1, 1, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (9, 0, N'9c76b896-fd6a-4beb-a95b-6b292f8c0a6f', CAST(N'2026-01-05T23:49:49.0000000' AS DateTime2), NULL, 1, NULL, N'נתחים', N'', 1, 2, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (10, 0, N'6eb7ff94-b9eb-4504-8901-1799a8df776c', CAST(N'2026-01-05T23:49:58.0000000' AS DateTime2), NULL, 1, NULL, N'נקניקיות', N'', 1, 3, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (11, 0, N'b087bf02-40dc-449c-8600-335e01f40c10', CAST(N'2026-01-05T23:50:04.0000000' AS DateTime2), NULL, 1, NULL, N'שיפודים', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (12, 1, N'f4753280-65f4-45d3-96c0-0aeed1dbf067', CAST(N'2026-01-05T23:50:14.0000000' AS DateTime2), CAST(N'2026-01-06T00:01:29.0000000' AS DateTime2), 1, NULL, N'שיפודים', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (13, 0, N'1cdc493f-09b2-4ff9-b366-d8f896b2fbcd', CAST(N'2026-01-05T23:55:25.0000000' AS DateTime2), NULL, 1, NULL, N'קבב והמבורגר', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (14, 0, N'6ec431c7-2501-4f36-b020-6ab7327719df', CAST(N'2026-01-05T23:55:32.0000000' AS DateTime2), NULL, 1, NULL, N'עצמות', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (15, 0, N'5b422fc0-99fa-4ef3-8a22-7e66962df8da', CAST(N'2026-01-05T23:55:38.0000000' AS DateTime2), NULL, 1, NULL, N'וואגיו', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (16, 0, N'def2668c-abbc-4b71-aff9-53ed6d144cb8', CAST(N'2026-01-05T23:55:46.0000000' AS DateTime2), NULL, 1, NULL, N'חלקי פנים', N'', 1, 4, 0)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (17, 0, N'10757fd3-ab26-45c0-9861-d05a83092955', CAST(N'2026-01-05T23:56:08.0000000' AS DateTime2), CAST(N'2026-01-05T23:56:21.0000000' AS DateTime2), 1, 1, N'אווז', N'', 5, NULL, NULL)
GO
INSERT [dbo].[GlobalCategory] ([Id], [IsDeleted], [GuidId], [CreationTime], [UpdatedDate], [CreationUserId], [UpdateUserId], [Name], [Description], [ParentGlobalCategoryId], [SortOrder], [ProductCount]) VALUES (18, 0, N'db7a55f6-a1a6-49f3-947b-a552a2838061', CAST(N'2026-01-05T23:56:14.0000000' AS DateTime2), NULL, 1, NULL, N'הודו', N'', 5, 1, 0)
GO
SET IDENTITY_INSERT [dbo].[GlobalCategory] OFF
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (1, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (2, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (3, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (4, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (5, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (6, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (7, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (8, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (9, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (10, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (11, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (12, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (13, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (14, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (15, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (16, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (17, 3)
GO
INSERT [dbo].[GlobalCategoryBusinessType] ([GlobalCategoryId], [BusinessTypeId]) VALUES (18, 3)
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
