USE [George.Dev]
GO
SET IDENTITY_INSERT [dbo].[Account] ON 
GO
INSERT [dbo].[Account] ([Id], [Name], [IsActive], [IsKosherShop], [AllowWeighted], [CreatedAt], [UpdatedAt], [StoreDomain]) VALUES (1, N'קצביית יוסי', 1, 1, 1, CAST(N'2025-10-25T01:45:04.8436138' AS DateTime2), NULL, NULL)
GO
SET IDENTITY_INSERT [dbo].[Account] OFF
GO
INSERT [dbo].[Role] ([Id], [Name]) VALUES (2, N'Admin')
GO
INSERT [dbo].[Role] ([Id], [Name]) VALUES (3, N'SubAdmin')
GO
INSERT [dbo].[Role] ([Id], [Name]) VALUES (1, N'SuperAdmin')
GO
INSERT [dbo].[UserStatus] ([Id], [Name]) VALUES (1, N'Active')
GO
INSERT [dbo].[UserStatus] ([Id], [Name]) VALUES (2, N'Blocked')
GO
INSERT [dbo].[UserStatus] ([Id], [Name]) VALUES (3, N'PendingInvite')
GO
SET IDENTITY_INSERT [dbo].[User] ON 
GO
INSERT [dbo].[User] ([Id], [StatusId], [RoleId], [FirstName], [LastName], [Email], [IsEmailVerified], [Password], [Otp], [LastLoginDate], [LockoutFailCount], [LockoutExpiration], [RefreshToken], [RefreshTokenExpiration], [IsMaster], [IsDeleted], [Phone]) VALUES (1, 1, 1, N'Platform', N'Owner', N'owner@butcher-platform.local', 1, N'P@ssw0rd!', NULL, CAST(N'2025-10-25T01:40:10.0000000' AS DateTime2), 0, NULL, NULL, NULL, 1, 0, NULL)
GO
INSERT [dbo].[User] ([Id], [StatusId], [RoleId], [FirstName], [LastName], [Email], [IsEmailVerified], [Password], [Otp], [LastLoginDate], [LockoutFailCount], [LockoutExpiration], [RefreshToken], [RefreshTokenExpiration], [IsMaster], [IsDeleted], [Phone]) VALUES (2, 1, 2, N'Yossi', N'Levi', N'yossi@meatshop.local', 1, N'P@ssw0rd!', NULL, NULL, 0, NULL, NULL, NULL, 0, 0, NULL)
GO
INSERT [dbo].[User] ([Id], [StatusId], [RoleId], [FirstName], [LastName], [Email], [IsEmailVerified], [Password], [Otp], [LastLoginDate], [LockoutFailCount], [LockoutExpiration], [RefreshToken], [RefreshTokenExpiration], [IsMaster], [IsDeleted], [Phone]) VALUES (3, 1, 3, N'Dana', N'Cutter', N'dana@meatshop.local', 1, N'P@ssw0rd!', NULL, NULL, 0, NULL, NULL, NULL, 0, 0, NULL)
GO
SET IDENTITY_INSERT [dbo].[User] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountUser] ON 
GO
INSERT [dbo].[AccountUser] ([Id], [AccountId], [UserId], [RoleId], [IsActive]) VALUES (1, 1, 2, 2, 1)
GO
INSERT [dbo].[AccountUser] ([Id], [AccountId], [UserId], [RoleId], [IsActive]) VALUES (2, 1, 3, 3, 1)
GO
SET IDENTITY_INSERT [dbo].[AccountUser] OFF
GO
SET IDENTITY_INSERT [dbo].[WizardSession] ON 
GO
INSERT [dbo].[WizardSession] ([Id], [AccountId], [StartedByUserId], [Step], [Status], [CreatedAt], [CompletedAt], [ContentOwner]) VALUES (1, 1, 2, 2, N'InProgress', CAST(N'2025-10-25T01:45:04.8459595' AS DateTime2), NULL, N'Company')
GO
SET IDENTITY_INSERT [dbo].[WizardSession] OFF
GO
SET IDENTITY_INSERT [dbo].[Category] ON 
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (1, N'בקר', N'beef', 1, 10)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (2, N'סטייקים', N'steaks', 1, 11)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (3, N'טחונים', N'ground', 1, 12)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (4, N'נתחים', N'roasts', 1, 13)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (5, N'עופות', N'poultry', 1, 20)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (6, N'דגים', N'fish', 1, 30)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (7, N'תבלינים', N'spices', 1, 40)
GO
INSERT [dbo].[Category] ([Id], [Name], [Slug], [IsActive], [SortOrder]) VALUES (8, N'מזווה', N'pantry', 1, 50)
GO
SET IDENTITY_INSERT [dbo].[Category] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountCategory] ON 
GO
INSERT [dbo].[AccountCategory] ([Id], [AccountId], [CategoryId], [ParentAccountCategoryId], [CustomName], [SortOrder], [IsEnabled]) VALUES (1, 1, 1, NULL, N'בקר טרי', 10, 1)
GO
INSERT [dbo].[AccountCategory] ([Id], [AccountId], [CategoryId], [ParentAccountCategoryId], [CustomName], [SortOrder], [IsEnabled]) VALUES (2, 1, 2, NULL, N'סטייקים פרימיום', 11, 1)
GO
INSERT [dbo].[AccountCategory] ([Id], [AccountId], [CategoryId], [ParentAccountCategoryId], [CustomName], [SortOrder], [IsEnabled]) VALUES (3, 1, 5, NULL, NULL, 20, 1)
GO
INSERT [dbo].[AccountCategory] ([Id], [AccountId], [CategoryId], [ParentAccountCategoryId], [CustomName], [SortOrder], [IsEnabled]) VALUES (4, 1, 7, NULL, NULL, 40, 0)
GO
SET IDENTITY_INSERT [dbo].[AccountCategory] OFF
GO
SET IDENTITY_INSERT [dbo].[Unit] ON 
GO
INSERT [dbo].[Unit] ([Id], [Name], [Code]) VALUES (1, N'Gram', N'g')
GO
INSERT [dbo].[Unit] ([Id], [Name], [Code]) VALUES (2, N'Kilogram', N'kg')
GO
INSERT [dbo].[Unit] ([Id], [Name], [Code]) VALUES (3, N'Unit', N'unit')
GO
INSERT [dbo].[Unit] ([Id], [Name], [Code]) VALUES (4, N'Pack', N'pack')
GO
INSERT [dbo].[Unit] ([Id], [Name], [Code]) VALUES (5, N'100 Grams', N'100g')
GO
SET IDENTITY_INSERT [dbo].[Unit] OFF
GO
SET IDENTITY_INSERT [dbo].[KosherStatus] ON 
GO
INSERT [dbo].[KosherStatus] ([Id], [Name]) VALUES (1, N'Kosher')
GO
INSERT [dbo].[KosherStatus] ([Id], [Name]) VALUES (2, N'NotKosher')
GO
INSERT [dbo].[KosherStatus] ([Id], [Name]) VALUES (3, N'Unknown')
GO
SET IDENTITY_INSERT [dbo].[KosherStatus] OFF
GO
SET IDENTITY_INSERT [dbo].[WeightPricingModel] ON 
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (1, N'ByWeight', N'מוצר שקיל לפי משקל')
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (2, N'ByUnitAvgWeight', N'מוצר שקיל לפי יחידה ממוצעת')
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (3, N'UnitSelectableWeights', N'מוצר שקיל לפי יחידה משקל משתנה (בחירות)')
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (4, N'UnitBySizeVariant', N'מוצר שקיל לפי יחידה וריאציה (גודל)')
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (5, N'BothWeightAndUnit', N'מוצר גם שקיל לפי משקל וגם לפי יחידה')
GO
INSERT [dbo].[WeightPricingModel] ([Id], [Code], [Name]) VALUES (6, N'Per100g', N'מוצר שקיל לפי 100 גרם')
GO
SET IDENTITY_INSERT [dbo].[WeightPricingModel] OFF
GO
SET IDENTITY_INSERT [dbo].[Brand] ON 
GO
INSERT [dbo].[Brand] ([Id], [Name], [IsActive]) VALUES (1, N'Mutti', 1)
GO
INSERT [dbo].[Brand] ([Id], [Name], [IsActive]) VALUES (2, N'Local Farm', 1)
GO
SET IDENTITY_INSERT [dbo].[Brand] OFF
GO
SET IDENTITY_INSERT [dbo].[Supplier] ON 
GO
INSERT [dbo].[Supplier] ([Id], [Name], [IsActive]) VALUES (1, N'Ristretto', 1)
GO
INSERT [dbo].[Supplier] ([Id], [Name], [IsActive]) VALUES (2, N'In-House Cutting Room', 1)
GO
SET IDENTITY_INSERT [dbo].[Supplier] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductType] ON 
GO
INSERT [dbo].[ProductType] ([Id], [Code], [Name]) VALUES (1, N'Simple', N'Simple Product')
GO
INSERT [dbo].[ProductType] ([Id], [Code], [Name]) VALUES (2, N'Variable', N'Variable Product')
GO
INSERT [dbo].[ProductType] ([Id], [Code], [Name]) VALUES (3, N'Bundle', N'Bundle Product')
GO
SET IDENTITY_INSERT [dbo].[ProductType] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductTemplate] ON 
GO
INSERT [dbo].[ProductTemplate] ([Id], [Sku], [Title], [ShortDescription], [DescriptionHtml], [BrandId], [SupplierId], [ProductTypeId], [WeightPricingModelId], [IsKosherDefault], [KosherStatusId], [BaseUnitPrice], [BaseUnitId], [BaseWeightGrams], [ShowPricePer100g], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'STEAK-ENTRECOTE', N'אנטריקוט טרי', N'סטייק אנטריקוט טרי ואיכותי', N'<p>אנטריקוט בקר טרי, נחתך במקום לפי המשקל שתבחר.</p>', 1, 1, 1, 1, 1, 1, CAST(149.90 AS Decimal(18, 2)), 2, NULL, 0, 1, CAST(N'2025-10-25T01:43:07.3595304' AS DateTime2), NULL)
GO
INSERT [dbo].[ProductTemplate] ([Id], [Sku], [Title], [ShortDescription], [DescriptionHtml], [BrandId], [SupplierId], [ProductTypeId], [WeightPricingModelId], [IsKosherDefault], [KosherStatusId], [BaseUnitPrice], [BaseUnitId], [BaseWeightGrams], [ShowPricePer100g], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (2, N'SPICE-PAPRIKA', N'פפריקה מתוקה', N'פפריקה מתוקה איכותית', N'<p>תבלין אדום, עדין ולא חריף. מתאים לכל סוגי הבשרים.</p>', 1, 1, 1, NULL, 1, 1, CAST(12.90 AS Decimal(18, 2)), 3, CAST(100.000 AS Decimal(18, 3)), 1, 1, CAST(N'2025-10-25T01:43:07.3595304' AS DateTime2), NULL)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplate] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProduct] ON 
GO
INSERT [dbo].[AccountProduct] ([Id], [AccountId], [ProductTemplateId], [IsEnabled], [EditingStatus], [Title], [ShortDescription], [DescriptionHtml], [BrandId], [SupplierId], [IsKosher], [KosherStatusId], [WeightPricingModelId], [ShowPricePer100g], [BaseUnitPrice], [BaseUnitId], [BaseWeightGrams], [StockQuantity], [Sku], [CreatedAt], [UpdatedAt], [WeightStepGrams]) VALUES (1, 1, 1, 1, N'Edited', N'אנטריקוט טרי', N'סטייק אנטריקוט טרי ואיכותי', N'<p>אנטריקוט בקר טרי, נחתך במקום לפי המשקל שתבחר.</p>', 1, 1, 1, 1, 1, 0, CAST(149.90 AS Decimal(18, 2)), 2, NULL, CAST(25.000 AS Decimal(18, 3)), N'STEAK-ENTRECOTE', CAST(N'2025-10-25T01:46:25.8831314' AS DateTime2), NULL, NULL)
GO
INSERT [dbo].[AccountProduct] ([Id], [AccountId], [ProductTemplateId], [IsEnabled], [EditingStatus], [Title], [ShortDescription], [DescriptionHtml], [BrandId], [SupplierId], [IsKosher], [KosherStatusId], [WeightPricingModelId], [ShowPricePer100g], [BaseUnitPrice], [BaseUnitId], [BaseWeightGrams], [StockQuantity], [Sku], [CreatedAt], [UpdatedAt], [WeightStepGrams]) VALUES (2, 1, 2, 0, N'NotEdited', N'פפריקה מתוקה', N'פפריקה מתוקה איכותית', N'<p>תבלין אדום, עדין ולא חריף. מתאים לכל סוגי הבשרים.</p>', 1, 1, 1, 1, NULL, 1, CAST(12.90 AS Decimal(18, 2)), 3, CAST(100.000 AS Decimal(18, 3)), NULL, N'SPICE-PAPRIKA', CAST(N'2025-10-25T01:46:25.8844830' AS DateTime2), NULL, NULL)
GO
SET IDENTITY_INSERT [dbo].[AccountProduct] OFF
GO
SET IDENTITY_INSERT [dbo].[EcomPlatform] ON 
GO
INSERT [dbo].[EcomPlatform] ([Id], [Code], [Name]) VALUES (1, N'WooCommerce', N'WooCommerce')
GO
SET IDENTITY_INSERT [dbo].[EcomPlatform] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountEcomCredential] ON 
GO
INSERT [dbo].[AccountEcomCredential] ([Id], [AccountId], [EcomPlatformId], [BaseUrl], [ApiKey], [ApiSecret], [IsActive], [CreatedAt]) VALUES (1, 1, 1, N'https://yossi-butcher-shop.com', N'ck_XXXXXXXXXXXXXXXX', N'cs_YYYYYYYYYYYYYYYY', 1, CAST(N'2025-10-25T01:52:41.4108266' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[AccountEcomCredential] OFF
GO
SET IDENTITY_INSERT [dbo].[SyncJob] ON 
GO
INSERT [dbo].[SyncJob] ([Id], [AccountId], [JobType], [Status], [RequestedBy], [CreatedAt], [StartedAt], [CompletedAt], [Message]) VALUES (1, 1, N'PushProducts', N'Pending', 2, CAST(N'2025-10-25T01:52:41.4168269' AS DateTime2), NULL, NULL, N'initial publish of 1 category and 1 product')
GO
SET IDENTITY_INSERT [dbo].[SyncJob] OFF
GO
SET IDENTITY_INSERT [dbo].[BusinessType] ON 
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (1, N'MEAT', N'בשר')
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (2, N'FISH', N'דגים')
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (3, N'SPICES', N'תבלינים')
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (4, N'PANTRY', N'מזווה')
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (5, N'PASTA', N'פסטות')
GO
INSERT [dbo].[BusinessType] ([Id], [Code], [Name]) VALUES (6, N'OTHER', N'אחר')
GO
SET IDENTITY_INSERT [dbo].[BusinessType] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductCategory] ON 
GO
INSERT [dbo].[AccountProductCategory] ([Id], [AccountProductId], [AccountCategoryId]) VALUES (1, 1, 2)
GO
SET IDENTITY_INSERT [dbo].[AccountProductCategory] OFF
GO
SET IDENTITY_INSERT [dbo].[EcomCategoryMap] ON 
GO
INSERT [dbo].[EcomCategoryMap] ([Id], [AccountCategoryId], [RemoteCategoryId], [SyncedAt]) VALUES (1, 2, N'42', CAST(N'2025-10-25T01:52:41.4118261' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[EcomCategoryMap] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductMedia] ON 
GO
INSERT [dbo].[AccountProductMedia] ([Id], [AccountProductId], [Url], [AltText], [SortOrder], [IsPrimary]) VALUES (1, 1, N'https://cdn.example.local/images/entrecote.jpg', N'אנטריקוט טרי', 10, 1)
GO
SET IDENTITY_INSERT [dbo].[AccountProductMedia] OFF
GO
SET IDENTITY_INSERT [dbo].[Attribute] ON 
GO
INSERT [dbo].[Attribute] ([Id], [Name], [DataType], [IsVariantAxis]) VALUES (1, N'גודל', N'Select', 1)
GO
INSERT [dbo].[Attribute] ([Id], [Name], [DataType], [IsVariantAxis]) VALUES (2, N'סוג נתח', N'Select', 1)
GO
INSERT [dbo].[Attribute] ([Id], [Name], [DataType], [IsVariantAxis]) VALUES (3, N'רמת חריפות', N'Select', 0)
GO
SET IDENTITY_INSERT [dbo].[Attribute] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductAttribute] ON 
GO
INSERT [dbo].[AccountProductAttribute] ([Id], [AccountProductId], [AttributeId], [IsVariantAxis]) VALUES (1, 1, 1, 1)
GO
INSERT [dbo].[AccountProductAttribute] ([Id], [AccountProductId], [AttributeId], [IsVariantAxis]) VALUES (2, 1, 2, 1)
GO
SET IDENTITY_INSERT [dbo].[AccountProductAttribute] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductVariant] ON 
GO
INSERT [dbo].[AccountProductVariant] ([Id], [AccountProductId], [VariantSku], [VariantTitle], [Price], [StockQuantity], [WeightGrams], [IsEnabled], [SortOrder]) VALUES (1, 1, N'STEAK-ENTRECOTE-300', N'אנטריקוט ~300 גרם', CAST(44.97 AS Decimal(18, 2)), CAST(10.000 AS Decimal(18, 3)), CAST(300.000 AS Decimal(18, 3)), 1, 10)
GO
INSERT [dbo].[AccountProductVariant] ([Id], [AccountProductId], [VariantSku], [VariantTitle], [Price], [StockQuantity], [WeightGrams], [IsEnabled], [SortOrder]) VALUES (2, 1, N'STEAK-ENTRECOTE-500', N'אנטריקוט ~500 גרם', CAST(74.95 AS Decimal(18, 2)), CAST(8.000 AS Decimal(18, 3)), CAST(500.000 AS Decimal(18, 3)), 1, 20)
GO
SET IDENTITY_INSERT [dbo].[AccountProductVariant] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductEditLog] ON 
GO
INSERT [dbo].[ProductEditLog] ([Id], [AccountProductId], [UserId], [FromStatus], [ToStatus], [Notes], [CreatedAt]) VALUES (1, 1, 3, N'NotEdited', N'Edited', N'עדכון תיאור ומחירים לפי המשקל בפועל', CAST(N'2025-10-25T01:46:25.9242682' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[ProductEditLog] OFF
GO
SET IDENTITY_INSERT [dbo].[EcomProductMap] ON 
GO
INSERT [dbo].[EcomProductMap] ([Id], [AccountProductId], [RemoteProductId], [SyncedAt]) VALUES (1, 1, N'1337', CAST(N'2025-10-25T01:52:41.4138259' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[EcomProductMap] OFF
GO
SET IDENTITY_INSERT [dbo].[AttributeOption] ON 
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (1, 1, N'קטן', 10)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (2, 1, N'בינוני', 20)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (3, 1, N'גדול', 30)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (4, 2, N'אנטריקוט', 10)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (5, 2, N'סינטה', 20)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (6, 2, N'צלעות', 30)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (7, 3, N'לא חריף', 10)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (8, 3, N'קל', 20)
GO
INSERT [dbo].[AttributeOption] ([Id], [AttributeId], [Value], [SortOrder]) VALUES (9, 3, N'חריף', 30)
GO
SET IDENTITY_INSERT [dbo].[AttributeOption] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductAttributeValue] ON 
GO
INSERT [dbo].[AccountProductAttributeValue] ([Id], [AccountProductAttributeId], [AttributeOptionId], [ValueText], [SortOrder]) VALUES (1, 1, 1, NULL, 0)
GO
INSERT [dbo].[AccountProductAttributeValue] ([Id], [AccountProductAttributeId], [AttributeOptionId], [ValueText], [SortOrder]) VALUES (2, 1, 2, NULL, 0)
GO
INSERT [dbo].[AccountProductAttributeValue] ([Id], [AccountProductAttributeId], [AttributeOptionId], [ValueText], [SortOrder]) VALUES (3, 1, 3, NULL, 0)
GO
SET IDENTITY_INSERT [dbo].[AccountProductAttributeValue] OFF
GO
SET IDENTITY_INSERT [dbo].[AuditLog] ON 
GO
INSERT [dbo].[AuditLog] ([Id], [UserId], [Action], [EntityName], [EntityId], [Payload], [CreatedAt]) VALUES (1, 3, N'AccountProduct.UpdatePrice', N'AccountProduct', 1, N'{"oldPricePerKg":149.90,"newPricePerKg":159.90}', CAST(N'2025-10-25T01:52:41.4098433' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[AuditLog] OFF
GO
SET IDENTITY_INSERT [dbo].[AccountProductVariantOption] ON 
GO
INSERT [dbo].[AccountProductVariantOption] ([Id], [AccountProductVariantId], [AttributeId], [AttributeOptionId]) VALUES (1, 1, 1, 1)
GO
INSERT [dbo].[AccountProductVariantOption] ([Id], [AccountProductVariantId], [AttributeId], [AttributeOptionId]) VALUES (2, 2, 1, 2)
GO
SET IDENTITY_INSERT [dbo].[AccountProductVariantOption] OFF
GO
SET IDENTITY_INSERT [dbo].[EcomVariantMap] ON 
GO
INSERT [dbo].[EcomVariantMap] ([Id], [AccountProductVariantId], [RemoteVariantId], [SyncedAt]) VALUES (1, 1, N'2001', CAST(N'2025-10-25T01:52:41.4148419' AS DateTime2))
GO
INSERT [dbo].[EcomVariantMap] ([Id], [AccountProductVariantId], [RemoteVariantId], [SyncedAt]) VALUES (2, 2, N'2002', CAST(N'2025-10-25T01:52:41.4148419' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[EcomVariantMap] OFF
GO
INSERT [dbo].[CategoryHierarchy] ([ParentCategoryId], [ChildCategoryId], [SortOrder]) VALUES (1, 2, 10)
GO
INSERT [dbo].[CategoryHierarchy] ([ParentCategoryId], [ChildCategoryId], [SortOrder]) VALUES (1, 3, 20)
GO
INSERT [dbo].[CategoryHierarchy] ([ParentCategoryId], [ChildCategoryId], [SortOrder]) VALUES (1, 4, 30)
GO
INSERT [dbo].[ProductTemplateCategory] ([ProductTemplateId], [CategoryId]) VALUES (1, 1)
GO
INSERT [dbo].[ProductTemplateCategory] ([ProductTemplateId], [CategoryId]) VALUES (1, 2)
GO
INSERT [dbo].[ProductTemplateCategory] ([ProductTemplateId], [CategoryId]) VALUES (2, 7)
GO
INSERT [dbo].[ProductTemplateCategory] ([ProductTemplateId], [CategoryId]) VALUES (2, 8)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateAttribute] ON 
GO
INSERT [dbo].[ProductTemplateAttribute] ([Id], [ProductTemplateId], [AttributeId], [IsVariantAxis]) VALUES (1, 1, 1, 1)
GO
INSERT [dbo].[ProductTemplateAttribute] ([Id], [ProductTemplateId], [AttributeId], [IsVariantAxis]) VALUES (2, 1, 2, 1)
GO
INSERT [dbo].[ProductTemplateAttribute] ([Id], [ProductTemplateId], [AttributeId], [IsVariantAxis]) VALUES (3, 2, 3, 0)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateAttribute] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateAttributeOption] ON 
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (1, 1, 1)
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (2, 1, 2)
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (3, 1, 3)
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (4, 3, 7)
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (5, 3, 8)
GO
INSERT [dbo].[ProductTemplateAttributeOption] ([Id], [ProductTemplateAttributeId], [AttributeOptionId]) VALUES (6, 3, 9)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateAttributeOption] OFF
GO
SET IDENTITY_INSERT [dbo].[SyncJobLog] ON 
GO
INSERT [dbo].[SyncJobLog] ([Id], [SyncJobId], [Level], [Message], [CreatedAt]) VALUES (1, 1, N'Info', N'Job created and queued for sync', CAST(N'2025-10-25T01:52:41.4179284' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[SyncJobLog] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateSelectableWeight] ON 
GO
INSERT [dbo].[ProductTemplateSelectableWeight] ([Id], [ProductTemplateId], [Label], [WeightGrams], [SortOrder]) VALUES (1, 1, N'300 גרם', CAST(300.000 AS Decimal(18, 3)), 10)
GO
INSERT [dbo].[ProductTemplateSelectableWeight] ([Id], [ProductTemplateId], [Label], [WeightGrams], [SortOrder]) VALUES (2, 1, N'500 גרם', CAST(500.000 AS Decimal(18, 3)), 20)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateSelectableWeight] OFF
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateMedia] ON 
GO
INSERT [dbo].[ProductTemplateMedia] ([Id], [ProductTemplateId], [Url], [AltText], [SortOrder], [IsPrimary]) VALUES (1, 1, N'https://cdn.example.local/images/entrecote.jpg', N'אנטריקוט טרי', 10, 1)
GO
INSERT [dbo].[ProductTemplateMedia] ([Id], [ProductTemplateId], [Url], [AltText], [SortOrder], [IsPrimary]) VALUES (2, 2, N'https://cdn.example.local/images/paprika.jpg', N'פפריקה מתוקה', 10, 1)
GO
SET IDENTITY_INSERT [dbo].[ProductTemplateMedia] OFF
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSAccessKey', N'***REDACTED***', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSBucket', N'teragon', NULL)
GO
INSERT [dbo].[SystemConfiguration] ([Key], [Value], [Description]) VALUES (N'AWSKeySecret', N'***REDACTED***', NULL)
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
