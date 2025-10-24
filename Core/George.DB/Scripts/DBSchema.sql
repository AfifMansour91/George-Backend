USE [George.Dev]
GO
/****** Object:  Table [dbo].[Account]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Account](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[IsKosherShop] [bit] NOT NULL,
	[AllowWeighted] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[StoreDomain] [nvarchar](250) NULL,
 CONSTRAINT [PK_Account] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProduct]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProduct](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ProductTemplateId] [bigint] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[EditingStatus] [nvarchar](30) NOT NULL,
	[Title] [nvarchar](300) NULL,
	[ShortDescription] [nvarchar](1000) NULL,
	[DescriptionHtml] [nvarchar](max) NULL,
	[BrandId] [int] NULL,
	[SupplierId] [int] NULL,
	[IsKosher] [bit] NULL,
	[KosherStatusId] [int] NULL,
	[WeightPricingModelId] [int] NULL,
	[ShowPricePer100g] [bit] NULL,
	[BaseUnitPrice] [decimal](18, 2) NULL,
	[BaseUnitId] [int] NULL,
	[BaseWeightGrams] [decimal](18, 3) NULL,
	[StockQuantity] [decimal](18, 3) NULL,
	[Sku] [nvarchar](100) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[WeightStepGrams] [decimal](18, 3) NULL,
 CONSTRAINT [PK_AccountProduct] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_AccountProductStatusSummary]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_AccountProductStatusSummary]
AS
SELECT
    ap.[AccountId],
    a.[Name]                        AS [AccountName],

    COUNT(*)                        AS [TotalProducts],
    SUM(CASE WHEN ap.[IsEnabled] = 1 THEN 1 ELSE 0 END) AS [EnabledProducts],
    SUM(CASE WHEN ap.[IsEnabled] = 0 THEN 1 ELSE 0 END) AS [DisabledProducts],

    SUM(CASE WHEN ap.[EditingStatus] = N'NotEdited' THEN 1 ELSE 0 END) AS [NotEditedCount],
    SUM(CASE WHEN ap.[EditingStatus] = N'Edited'    THEN 1 ELSE 0 END) AS [EditedCount],
    SUM(CASE WHEN ap.[EditingStatus] = N'Published' THEN 1 ELSE 0 END) AS [PublishedCount],

    MIN(ap.[CreatedAt])             AS [FirstProductCreatedAt],
    MAX(ap.[UpdatedAt])             AS [LastProductUpdatedAt]
FROM [dbo].[AccountProduct] ap
JOIN [dbo].[Account] a
  ON a.[Id] = ap.[AccountId]
GROUP BY
    ap.[AccountId],
    a.[Name];
GO
/****** Object:  Table [dbo].[AccountCategory]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountCategory](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[CategoryId] [int] NOT NULL,
	[ParentAccountCategoryId] [bigint] NULL,
	[CustomName] [nvarchar](200) NULL,
	[SortOrder] [int] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_AccountCategory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Category]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Category](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Slug] [nvarchar](200) NULL,
	[IsActive] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_Category] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_AccountCategoryTree]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_AccountCategoryTree]
AS
SELECT
    ac.[Id]                          AS [AccountCategoryId],
    ac.[AccountId],
    ac.[ParentAccountCategoryId],
    ac.[SortOrder],
    ac.[IsEnabled],
    COALESCE(ac.[CustomName], c.[Name]) AS [DisplayName],
    c.[Name]                          AS [BaseName],
    c.[Slug]                          AS [BaseSlug]
FROM [dbo].[AccountCategory] ac
JOIN [dbo].[Category] c
  ON c.[Id] = ac.[CategoryId];
GO
/****** Object:  Table [dbo].[AccountProductCategory]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductCategory](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[AccountCategoryId] [bigint] NOT NULL,
 CONSTRAINT [PK_AccountProductCategory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductMedia]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductMedia](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[Url] [nvarchar](1000) NOT NULL,
	[AltText] [nvarchar](300) NULL,
	[SortOrder] [int] NOT NULL,
	[IsPrimary] [bit] NOT NULL,
 CONSTRAINT [PK_AccountProductMedia] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Unit]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Unit](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Code] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_Unit] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Unit_Code] UNIQUE NONCLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[KosherStatus]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KosherStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_KosherStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WeightPricingModel]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WeightPricingModel](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](40) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_WeightPricingModel] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplate]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplate](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Sku] [nvarchar](100) NULL,
	[Title] [nvarchar](300) NOT NULL,
	[ShortDescription] [nvarchar](1000) NULL,
	[DescriptionHtml] [nvarchar](max) NULL,
	[BrandId] [int] NULL,
	[SupplierId] [int] NULL,
	[ProductTypeId] [int] NOT NULL,
	[WeightPricingModelId] [int] NULL,
	[IsKosherDefault] [bit] NOT NULL,
	[KosherStatusId] [int] NULL,
	[BaseUnitPrice] [decimal](18, 2) NULL,
	[BaseUnitId] [int] NULL,
	[BaseWeightGrams] [decimal](18, 3) NULL,
	[ShowPricePer100g] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_ProductTemplate] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_AccountProductList]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_AccountProductList]
AS
WITH catmap AS (
    SELECT
        apc.[AccountProductId],
        STUFF((
            SELECT N', ' + COALESCE(ac.[CustomName], c.[Name])
            FROM [dbo].[AccountProductCategory] apc2
            JOIN [dbo].[AccountCategory] ac
              ON ac.[Id] = apc2.[AccountCategoryId]
            JOIN [dbo].[Category] c
              ON c.[Id] = ac.[CategoryId]
            WHERE apc2.[AccountProductId] = apc.[AccountProductId]
              AND ac.[IsEnabled] = 1
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, N'') AS [CategoryNames]
    FROM [dbo].[AccountProductCategory] apc
    GROUP BY apc.[AccountProductId]
),
primary_image AS (
    SELECT
        apm.[AccountProductId],
        apm.[Url] AS [PrimaryImageUrl]
    FROM [dbo].[AccountProductMedia] apm
    WHERE apm.[IsPrimary] = 1
)
SELECT
    ap.[Id]                        AS [AccountProductId],
    ap.[AccountId],
    a.[Name]                       AS [AccountName],

    COALESCE(ap.[Title], pt.[Title]) AS [DisplayTitle],
    ap.[Sku],
    ap.[IsEnabled],
    ap.[EditingStatus],

    ap.[BaseUnitPrice],
    ap.[StockQuantity],
    u.[Code]                      AS [BaseUnitCode],

    ap.[IsKosher],
    ks.[Name]                     AS [KosherStatusName],

    wpm.[Code]                    AS [WeightModelCode],
    wpm.[Name]                    AS [WeightModelName],

    catmap.[CategoryNames],
    img.[PrimaryImageUrl],

    ap.[UpdatedAt],
    ap.[CreatedAt]

FROM [dbo].[AccountProduct] ap
JOIN [dbo].[Account] a
  ON a.[Id] = ap.[AccountId]
JOIN [dbo].[ProductTemplate] pt
  ON pt.[Id] = ap.[ProductTemplateId]
LEFT JOIN [dbo].[Unit] u
  ON u.[Id] = ap.[BaseUnitId]
LEFT JOIN [dbo].[KosherStatus] ks
  ON ks.[Id] = ap.[KosherStatusId]
LEFT JOIN [dbo].[WeightPricingModel] wpm
  ON wpm.[Id] = ap.[WeightPricingModelId]
LEFT JOIN catmap
  ON catmap.[AccountProductId] = ap.[Id]
LEFT JOIN primary_image img
  ON img.[AccountProductId] = ap.[Id];
GO
/****** Object:  Table [dbo].[AccountBusinessType]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountBusinessType](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[BusinessTypeId] [int] NOT NULL,
	[IsSelected] [bit] NOT NULL,
 CONSTRAINT [PK_AccountBusinessType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountEcomCredential]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountEcomCredential](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[EcomPlatformId] [int] NOT NULL,
	[BaseUrl] [nvarchar](300) NOT NULL,
	[ApiKey] [nvarchar](300) NOT NULL,
	[ApiSecret] [nvarchar](300) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AccountEcomCredential] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductAttribute]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductAttribute](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[AttributeId] [int] NOT NULL,
	[IsVariantAxis] [bit] NOT NULL,
 CONSTRAINT [PK_AccountProductAttribute] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductAttributeValue]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductAttributeValue](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductAttributeId] [bigint] NOT NULL,
	[AttributeOptionId] [int] NULL,
	[ValueText] [nvarchar](500) NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_AccountProductAttributeValue] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductImportStaging]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductImportStaging](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[SourceFileName] [nvarchar](255) NULL,
	[RowNumber] [int] NOT NULL,
	[RawName] [nvarchar](300) NULL,
	[RawDescription] [nvarchar](max) NULL,
	[RawSku] [nvarchar](100) NULL,
	[RawImageUrl] [nvarchar](1000) NULL,
	[RawPrice] [decimal](18, 2) NULL,
	[RawWeightInfo] [nvarchar](200) NULL,
	[MatchedProductTemplateId] [bigint] NULL,
	[MatchConfidence] [decimal](5, 2) NULL,
	[UseClientImage] [bit] NOT NULL,
	[Status] [nvarchar](30) NOT NULL,
 CONSTRAINT [PK_AccountProductImportStaging] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductVariant]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductVariant](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[VariantSku] [nvarchar](100) NULL,
	[VariantTitle] [nvarchar](300) NULL,
	[Price] [decimal](18, 2) NULL,
	[StockQuantity] [decimal](18, 3) NULL,
	[WeightGrams] [decimal](18, 3) NULL,
	[IsEnabled] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_AccountProductVariant] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountProductVariantOption]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountProductVariantOption](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductVariantId] [bigint] NOT NULL,
	[AttributeId] [int] NOT NULL,
	[AttributeOptionId] [int] NOT NULL,
 CONSTRAINT [PK_AccountProductVariantOption] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountUser]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountUser](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[UserId] [int] NOT NULL,
	[RoleId] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_AccountUser] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Attribute]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Attribute](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](150) NOT NULL,
	[DataType] [nvarchar](30) NOT NULL,
	[IsVariantAxis] [bit] NOT NULL,
 CONSTRAINT [PK_Attribute] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AttributeOption]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttributeOption](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AttributeId] [int] NOT NULL,
	[Value] [nvarchar](150) NOT NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_AttributeOption] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuditLog]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditLog](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NULL,
	[Action] [nvarchar](200) NOT NULL,
	[EntityName] [nvarchar](100) NOT NULL,
	[EntityId] [bigint] NULL,
	[Payload] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Brand]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Brand](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_Brand] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessType]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_BusinessType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CategoryHierarchy]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CategoryHierarchy](
	[ParentCategoryId] [int] NOT NULL,
	[ChildCategoryId] [int] NOT NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_CategoryHierarchy] PRIMARY KEY CLUSTERED 
(
	[ParentCategoryId] ASC,
	[ChildCategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EcomCategoryMap]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcomCategoryMap](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountCategoryId] [bigint] NOT NULL,
	[RemoteCategoryId] [nvarchar](100) NOT NULL,
	[SyncedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EcomCategoryMap] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EcomPlatform]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcomPlatform](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_EcomPlatform] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EcomProductMap]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcomProductMap](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[RemoteProductId] [nvarchar](100) NOT NULL,
	[SyncedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EcomProductMap] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EcomVariantMap]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcomVariantMap](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductVariantId] [bigint] NOT NULL,
	[RemoteVariantId] [nvarchar](100) NOT NULL,
	[SyncedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EcomVariantMap] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductEditLog]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductEditLog](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountProductId] [bigint] NOT NULL,
	[UserId] [int] NOT NULL,
	[FromStatus] [nvarchar](30) NOT NULL,
	[ToStatus] [nvarchar](30) NOT NULL,
	[Notes] [nvarchar](1000) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_ProductEditLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateAttribute]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateAttribute](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ProductTemplateId] [bigint] NOT NULL,
	[AttributeId] [int] NOT NULL,
	[IsVariantAxis] [bit] NOT NULL,
 CONSTRAINT [PK_ProductTemplateAttribute] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateAttributeOption]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateAttributeOption](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ProductTemplateAttributeId] [bigint] NOT NULL,
	[AttributeOptionId] [int] NOT NULL,
 CONSTRAINT [PK_ProductTemplateAttributeOption] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateBusinessType]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateBusinessType](
	[ProductTemplateId] [bigint] NOT NULL,
	[BusinessTypeId] [int] NOT NULL,
 CONSTRAINT [PK_ProductTemplateBusinessType] PRIMARY KEY CLUSTERED 
(
	[ProductTemplateId] ASC,
	[BusinessTypeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateCategory]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateCategory](
	[ProductTemplateId] [bigint] NOT NULL,
	[CategoryId] [int] NOT NULL,
 CONSTRAINT [PK_ProductTemplateCategory] PRIMARY KEY CLUSTERED 
(
	[ProductTemplateId] ASC,
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateMedia]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateMedia](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ProductTemplateId] [bigint] NOT NULL,
	[Url] [nvarchar](1000) NOT NULL,
	[AltText] [nvarchar](300) NULL,
	[SortOrder] [int] NOT NULL,
	[IsPrimary] [bit] NOT NULL,
 CONSTRAINT [PK_ProductTemplateMedia] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTemplateSelectableWeight]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTemplateSelectableWeight](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ProductTemplateId] [bigint] NOT NULL,
	[Label] [nvarchar](100) NOT NULL,
	[WeightGrams] [decimal](18, 3) NOT NULL,
	[SortOrder] [int] NOT NULL,
 CONSTRAINT [PK_ProductTemplateSelectableWeight] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductType]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](40) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_ProductType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Role]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Role](
	[Id] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Supplier]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Supplier](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncJob]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncJob](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[JobType] [nvarchar](50) NOT NULL,
	[Status] [nvarchar](30) NOT NULL,
	[RequestedBy] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[StartedAt] [datetime2](7) NULL,
	[CompletedAt] [datetime2](7) NULL,
	[Message] [nvarchar](2000) NULL,
 CONSTRAINT [PK_SyncJob] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncJobLog]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncJobLog](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[SyncJobId] [bigint] NOT NULL,
	[Level] [nvarchar](20) NOT NULL,
	[Message] [nvarchar](2000) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SyncJobLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SystemConfiguration]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SystemConfiguration](
	[Key] [nvarchar](100) NOT NULL,
	[Value] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[User]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[User](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StatusId] [int] NOT NULL,
	[RoleId] [int] NOT NULL,
	[FirstName] [nvarchar](50) NOT NULL,
	[LastName] [nvarchar](50) NOT NULL,
	[FullName]  AS (([Firstname]+' ')+[LastName]) PERSISTED NOT NULL,
	[Email] [nvarchar](250) NULL,
	[IsEmailVerified] [bit] NOT NULL,
	[Password] [nvarchar](250) NULL,
	[Otp] [nvarchar](50) NULL,
	[LastLoginDate] [datetime2](0) NULL,
	[LockoutFailCount] [int] NOT NULL,
	[LockoutExpiration] [datetime2](0) NULL,
	[RefreshToken] [nvarchar](250) NULL,
	[RefreshTokenExpiration] [datetime2](0) NULL,
	[IsMaster] [bit] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[Phone] [nvarchar](50) NULL,
 CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserStatus]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserStatus](
	[Id] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_UserStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WizardSession]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WizardSession](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[StartedByUserId] [int] NOT NULL,
	[Step] [int] NOT NULL,
	[Status] [nvarchar](30) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CompletedAt] [datetime2](7) NULL,
	[ContentOwner] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_WizardSession] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Role_Name_Unique]    Script Date: 10/25/2025 2:14:45 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Role_Name_Unique] ON [dbo].[Role]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FK_User_UserStatus_StatusId]    Script Date: 10/25/2025 2:14:45 AM ******/
CREATE NONCLUSTERED INDEX [IX_FK_User_UserStatus_StatusId] ON [dbo].[User]
(
	[StatusId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_User_Email_Unique]    Script Date: 10/25/2025 2:14:45 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email_Unique] ON [dbo].[User]
(
	[Email] ASC
)
WHERE ([IsDeleted]=(0) AND [Email] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_UserStatus_Name_Unique]    Script Date: 10/25/2025 2:14:45 AM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_UserStatus_Name_Unique] ON [dbo].[UserStatus]
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_IsKosherShop]  DEFAULT ((1)) FOR [IsKosherShop]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_AllowWeighted]  DEFAULT ((1)) FOR [AllowWeighted]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AccountBusinessType] ADD  CONSTRAINT [DF_AccountBusinessType_IsSelected]  DEFAULT ((1)) FOR [IsSelected]
GO
ALTER TABLE [dbo].[AccountCategory] ADD  CONSTRAINT [DF_AccountCategory_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[AccountCategory] ADD  CONSTRAINT [DF_AccountCategory_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[AccountEcomCredential] ADD  CONSTRAINT [DF_AccountEcomCredential_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[AccountEcomCredential] ADD  CONSTRAINT [DF_AccountEcomCredential_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AccountProduct] ADD  CONSTRAINT [DF_AccountProduct_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[AccountProduct] ADD  CONSTRAINT [DF_AccountProduct_EditingStatus]  DEFAULT ('NotEdited') FOR [EditingStatus]
GO
ALTER TABLE [dbo].[AccountProduct] ADD  CONSTRAINT [DF_AccountProduct_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AccountProductAttribute] ADD  CONSTRAINT [DF_AccountProductAttribute_IsVariantAxis]  DEFAULT ((0)) FOR [IsVariantAxis]
GO
ALTER TABLE [dbo].[AccountProductAttributeValue] ADD  CONSTRAINT [DF_AccountProductAttributeValue_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[AccountProductImportStaging] ADD  CONSTRAINT [DF_AccountProductImportStaging_UseClientImage]  DEFAULT ((0)) FOR [UseClientImage]
GO
ALTER TABLE [dbo].[AccountProductImportStaging] ADD  CONSTRAINT [DF_AccountProductImportStaging_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[AccountProductMedia] ADD  CONSTRAINT [DF_AccountProductMedia_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[AccountProductMedia] ADD  CONSTRAINT [DF_AccountProductMedia_IsPrimary]  DEFAULT ((0)) FOR [IsPrimary]
GO
ALTER TABLE [dbo].[AccountProductVariant] ADD  CONSTRAINT [DF_AccountProductVariant_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[AccountProductVariant] ADD  CONSTRAINT [DF_AccountProductVariant_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[AccountUser] ADD  CONSTRAINT [DF_AccountUser_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Attribute] ADD  CONSTRAINT [DF_Attribute_IsVariantAxis]  DEFAULT ((0)) FOR [IsVariantAxis]
GO
ALTER TABLE [dbo].[AttributeOption] ADD  CONSTRAINT [DF_AttributeOption_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[AuditLog] ADD  CONSTRAINT [DF_AuditLog_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Brand] ADD  CONSTRAINT [DF_Brand_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[CategoryHierarchy] ADD  CONSTRAINT [DF_CategoryHierarchy_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[EcomCategoryMap] ADD  CONSTRAINT [DF_EcomCategoryMap_SyncedAt]  DEFAULT (sysdatetime()) FOR [SyncedAt]
GO
ALTER TABLE [dbo].[EcomProductMap] ADD  CONSTRAINT [DF_EcomProductMap_SyncedAt]  DEFAULT (sysdatetime()) FOR [SyncedAt]
GO
ALTER TABLE [dbo].[EcomVariantMap] ADD  CONSTRAINT [DF_EcomVariantMap_SyncedAt]  DEFAULT (sysdatetime()) FOR [SyncedAt]
GO
ALTER TABLE [dbo].[ProductEditLog] ADD  CONSTRAINT [DF_ProductEditLog_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ProductTemplate] ADD  CONSTRAINT [DF_ProductTemplate_IsKosherDefault]  DEFAULT ((1)) FOR [IsKosherDefault]
GO
ALTER TABLE [dbo].[ProductTemplate] ADD  CONSTRAINT [DF_ProductTemplate_ShowPricePer100g]  DEFAULT ((0)) FOR [ShowPricePer100g]
GO
ALTER TABLE [dbo].[ProductTemplate] ADD  CONSTRAINT [DF_ProductTemplate_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ProductTemplate] ADD  CONSTRAINT [DF_ProductTemplate_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ProductTemplateAttribute] ADD  CONSTRAINT [DF_ProductTemplateAttribute_IsVariantAxis]  DEFAULT ((0)) FOR [IsVariantAxis]
GO
ALTER TABLE [dbo].[ProductTemplateMedia] ADD  CONSTRAINT [DF_ProductTemplateMedia_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[ProductTemplateMedia] ADD  CONSTRAINT [DF_ProductTemplateMedia_IsPrimary]  DEFAULT ((0)) FOR [IsPrimary]
GO
ALTER TABLE [dbo].[ProductTemplateSelectableWeight] ADD  CONSTRAINT [DF_ProductTemplateSelectableWeight_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[SyncJob] ADD  CONSTRAINT [DF_SyncJob_Status]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[SyncJob] ADD  CONSTRAINT [DF_SyncJob_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[SyncJobLog] ADD  CONSTRAINT [DF_SyncJobLog_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsEmailVerified]  DEFAULT ((0)) FOR [IsEmailVerified]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_LockoutFailCount]  DEFAULT ((0)) FOR [LockoutFailCount]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsMaster]  DEFAULT ((0)) FOR [IsMaster]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[WizardSession] ADD  CONSTRAINT [DF_WizardSession_Step]  DEFAULT ((1)) FOR [Step]
GO
ALTER TABLE [dbo].[WizardSession] ADD  CONSTRAINT [DF_WizardSession_Status]  DEFAULT ('InProgress') FOR [Status]
GO
ALTER TABLE [dbo].[WizardSession] ADD  CONSTRAINT [DF_WizardSession_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[WizardSession] ADD  CONSTRAINT [DF_WizardSession_ContentOwner]  DEFAULT ('Company') FOR [ContentOwner]
GO
ALTER TABLE [dbo].[AccountBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_AccountBusinessType_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountBusinessType] CHECK CONSTRAINT [FK_AccountBusinessType_Account]
GO
ALTER TABLE [dbo].[AccountBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_AccountBusinessType_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[AccountBusinessType] CHECK CONSTRAINT [FK_AccountBusinessType_BusinessType]
GO
ALTER TABLE [dbo].[AccountCategory]  WITH CHECK ADD  CONSTRAINT [FK_AccountCategory_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountCategory] CHECK CONSTRAINT [FK_AccountCategory_Account]
GO
ALTER TABLE [dbo].[AccountCategory]  WITH CHECK ADD  CONSTRAINT [FK_AccountCategory_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[AccountCategory] CHECK CONSTRAINT [FK_AccountCategory_Category]
GO
ALTER TABLE [dbo].[AccountCategory]  WITH CHECK ADD  CONSTRAINT [FK_AccountCategory_Parent] FOREIGN KEY([ParentAccountCategoryId])
REFERENCES [dbo].[AccountCategory] ([Id])
GO
ALTER TABLE [dbo].[AccountCategory] CHECK CONSTRAINT [FK_AccountCategory_Parent]
GO
ALTER TABLE [dbo].[AccountEcomCredential]  WITH CHECK ADD  CONSTRAINT [FK_AccountEcomCredential_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountEcomCredential] CHECK CONSTRAINT [FK_AccountEcomCredential_Account]
GO
ALTER TABLE [dbo].[AccountEcomCredential]  WITH CHECK ADD  CONSTRAINT [FK_AccountEcomCredential_EcomPlatform] FOREIGN KEY([EcomPlatformId])
REFERENCES [dbo].[EcomPlatform] ([Id])
GO
ALTER TABLE [dbo].[AccountEcomCredential] CHECK CONSTRAINT [FK_AccountEcomCredential_EcomPlatform]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_Account]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_Brand] FOREIGN KEY([BrandId])
REFERENCES [dbo].[Brand] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_Brand]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_KosherStatus] FOREIGN KEY([KosherStatusId])
REFERENCES [dbo].[KosherStatus] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_KosherStatus]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_ProductTemplate]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_Supplier] FOREIGN KEY([SupplierId])
REFERENCES [dbo].[Supplier] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_Supplier]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_Unit] FOREIGN KEY([BaseUnitId])
REFERENCES [dbo].[Unit] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_Unit]
GO
ALTER TABLE [dbo].[AccountProduct]  WITH CHECK ADD  CONSTRAINT [FK_AccountProduct_WeightPricingModel] FOREIGN KEY([WeightPricingModelId])
REFERENCES [dbo].[WeightPricingModel] ([Id])
GO
ALTER TABLE [dbo].[AccountProduct] CHECK CONSTRAINT [FK_AccountProduct_WeightPricingModel]
GO
ALTER TABLE [dbo].[AccountProductAttribute]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductAttribute_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[AccountProductAttribute] CHECK CONSTRAINT [FK_AccountProductAttribute_AccountProduct]
GO
ALTER TABLE [dbo].[AccountProductAttribute]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductAttribute_Attribute] FOREIGN KEY([AttributeId])
REFERENCES [dbo].[Attribute] ([Id])
GO
ALTER TABLE [dbo].[AccountProductAttribute] CHECK CONSTRAINT [FK_AccountProductAttribute_Attribute]
GO
ALTER TABLE [dbo].[AccountProductAttributeValue]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductAttributeValue_AccountProductAttribute] FOREIGN KEY([AccountProductAttributeId])
REFERENCES [dbo].[AccountProductAttribute] ([Id])
GO
ALTER TABLE [dbo].[AccountProductAttributeValue] CHECK CONSTRAINT [FK_AccountProductAttributeValue_AccountProductAttribute]
GO
ALTER TABLE [dbo].[AccountProductAttributeValue]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductAttributeValue_AttributeOption] FOREIGN KEY([AttributeOptionId])
REFERENCES [dbo].[AttributeOption] ([Id])
GO
ALTER TABLE [dbo].[AccountProductAttributeValue] CHECK CONSTRAINT [FK_AccountProductAttributeValue_AttributeOption]
GO
ALTER TABLE [dbo].[AccountProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductCategory_AccountCategory] FOREIGN KEY([AccountCategoryId])
REFERENCES [dbo].[AccountCategory] ([Id])
GO
ALTER TABLE [dbo].[AccountProductCategory] CHECK CONSTRAINT [FK_AccountProductCategory_AccountCategory]
GO
ALTER TABLE [dbo].[AccountProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductCategory_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[AccountProductCategory] CHECK CONSTRAINT [FK_AccountProductCategory_AccountProduct]
GO
ALTER TABLE [dbo].[AccountProductImportStaging]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductImportStaging_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountProductImportStaging] CHECK CONSTRAINT [FK_AccountProductImportStaging_Account]
GO
ALTER TABLE [dbo].[AccountProductImportStaging]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductImportStaging_ProductTemplate] FOREIGN KEY([MatchedProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[AccountProductImportStaging] CHECK CONSTRAINT [FK_AccountProductImportStaging_ProductTemplate]
GO
ALTER TABLE [dbo].[AccountProductMedia]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductMedia_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[AccountProductMedia] CHECK CONSTRAINT [FK_AccountProductMedia_AccountProduct]
GO
ALTER TABLE [dbo].[AccountProductVariant]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductVariant_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[AccountProductVariant] CHECK CONSTRAINT [FK_AccountProductVariant_AccountProduct]
GO
ALTER TABLE [dbo].[AccountProductVariantOption]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductVariantOption_AccountProductVariant] FOREIGN KEY([AccountProductVariantId])
REFERENCES [dbo].[AccountProductVariant] ([Id])
GO
ALTER TABLE [dbo].[AccountProductVariantOption] CHECK CONSTRAINT [FK_AccountProductVariantOption_AccountProductVariant]
GO
ALTER TABLE [dbo].[AccountProductVariantOption]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductVariantOption_Attribute] FOREIGN KEY([AttributeId])
REFERENCES [dbo].[Attribute] ([Id])
GO
ALTER TABLE [dbo].[AccountProductVariantOption] CHECK CONSTRAINT [FK_AccountProductVariantOption_Attribute]
GO
ALTER TABLE [dbo].[AccountProductVariantOption]  WITH CHECK ADD  CONSTRAINT [FK_AccountProductVariantOption_AttributeOption] FOREIGN KEY([AttributeOptionId])
REFERENCES [dbo].[AttributeOption] ([Id])
GO
ALTER TABLE [dbo].[AccountProductVariantOption] CHECK CONSTRAINT [FK_AccountProductVariantOption_AttributeOption]
GO
ALTER TABLE [dbo].[AccountUser]  WITH CHECK ADD  CONSTRAINT [FK_AccountUser_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[AccountUser] CHECK CONSTRAINT [FK_AccountUser_Account]
GO
ALTER TABLE [dbo].[AccountUser]  WITH CHECK ADD  CONSTRAINT [FK_AccountUser_Role] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Role] ([Id])
GO
ALTER TABLE [dbo].[AccountUser] CHECK CONSTRAINT [FK_AccountUser_Role]
GO
ALTER TABLE [dbo].[AccountUser]  WITH CHECK ADD  CONSTRAINT [FK_AccountUser_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[AccountUser] CHECK CONSTRAINT [FK_AccountUser_User]
GO
ALTER TABLE [dbo].[AttributeOption]  WITH CHECK ADD  CONSTRAINT [FK_AttributeOption_Attribute] FOREIGN KEY([AttributeId])
REFERENCES [dbo].[Attribute] ([Id])
GO
ALTER TABLE [dbo].[AttributeOption] CHECK CONSTRAINT [FK_AttributeOption_Attribute]
GO
ALTER TABLE [dbo].[AuditLog]  WITH CHECK ADD  CONSTRAINT [FK_AuditLog_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[AuditLog] CHECK CONSTRAINT [FK_AuditLog_User]
GO
ALTER TABLE [dbo].[CategoryHierarchy]  WITH CHECK ADD  CONSTRAINT [FK_CategoryHierarchy_Child] FOREIGN KEY([ChildCategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[CategoryHierarchy] CHECK CONSTRAINT [FK_CategoryHierarchy_Child]
GO
ALTER TABLE [dbo].[CategoryHierarchy]  WITH CHECK ADD  CONSTRAINT [FK_CategoryHierarchy_Parent] FOREIGN KEY([ParentCategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[CategoryHierarchy] CHECK CONSTRAINT [FK_CategoryHierarchy_Parent]
GO
ALTER TABLE [dbo].[EcomCategoryMap]  WITH CHECK ADD  CONSTRAINT [FK_EcomCategoryMap_AccountCategory] FOREIGN KEY([AccountCategoryId])
REFERENCES [dbo].[AccountCategory] ([Id])
GO
ALTER TABLE [dbo].[EcomCategoryMap] CHECK CONSTRAINT [FK_EcomCategoryMap_AccountCategory]
GO
ALTER TABLE [dbo].[EcomProductMap]  WITH CHECK ADD  CONSTRAINT [FK_EcomProductMap_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[EcomProductMap] CHECK CONSTRAINT [FK_EcomProductMap_AccountProduct]
GO
ALTER TABLE [dbo].[EcomVariantMap]  WITH CHECK ADD  CONSTRAINT [FK_EcomVariantMap_AccountProductVariant] FOREIGN KEY([AccountProductVariantId])
REFERENCES [dbo].[AccountProductVariant] ([Id])
GO
ALTER TABLE [dbo].[EcomVariantMap] CHECK CONSTRAINT [FK_EcomVariantMap_AccountProductVariant]
GO
ALTER TABLE [dbo].[ProductEditLog]  WITH CHECK ADD  CONSTRAINT [FK_ProductEditLog_AccountProduct] FOREIGN KEY([AccountProductId])
REFERENCES [dbo].[AccountProduct] ([Id])
GO
ALTER TABLE [dbo].[ProductEditLog] CHECK CONSTRAINT [FK_ProductEditLog_AccountProduct]
GO
ALTER TABLE [dbo].[ProductEditLog]  WITH CHECK ADD  CONSTRAINT [FK_ProductEditLog_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[ProductEditLog] CHECK CONSTRAINT [FK_ProductEditLog_User]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_Brand] FOREIGN KEY([BrandId])
REFERENCES [dbo].[Brand] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_Brand]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_KosherStatus] FOREIGN KEY([KosherStatusId])
REFERENCES [dbo].[KosherStatus] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_KosherStatus]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_ProductType] FOREIGN KEY([ProductTypeId])
REFERENCES [dbo].[ProductType] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_ProductType]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_Supplier] FOREIGN KEY([SupplierId])
REFERENCES [dbo].[Supplier] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_Supplier]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_Unit] FOREIGN KEY([BaseUnitId])
REFERENCES [dbo].[Unit] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_Unit]
GO
ALTER TABLE [dbo].[ProductTemplate]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplate_WeightPricingModel] FOREIGN KEY([WeightPricingModelId])
REFERENCES [dbo].[WeightPricingModel] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplate] CHECK CONSTRAINT [FK_ProductTemplate_WeightPricingModel]
GO
ALTER TABLE [dbo].[ProductTemplateAttribute]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateAttribute_Attribute] FOREIGN KEY([AttributeId])
REFERENCES [dbo].[Attribute] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateAttribute] CHECK CONSTRAINT [FK_ProductTemplateAttribute_Attribute]
GO
ALTER TABLE [dbo].[ProductTemplateAttribute]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateAttribute_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateAttribute] CHECK CONSTRAINT [FK_ProductTemplateAttribute_ProductTemplate]
GO
ALTER TABLE [dbo].[ProductTemplateAttributeOption]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateAttributeOption_AttributeOption] FOREIGN KEY([AttributeOptionId])
REFERENCES [dbo].[AttributeOption] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateAttributeOption] CHECK CONSTRAINT [FK_ProductTemplateAttributeOption_AttributeOption]
GO
ALTER TABLE [dbo].[ProductTemplateAttributeOption]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateAttributeOption_ProductTemplateAttribute] FOREIGN KEY([ProductTemplateAttributeId])
REFERENCES [dbo].[ProductTemplateAttribute] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateAttributeOption] CHECK CONSTRAINT [FK_ProductTemplateAttributeOption_ProductTemplateAttribute]
GO
ALTER TABLE [dbo].[ProductTemplateBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateBusinessType_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateBusinessType] CHECK CONSTRAINT [FK_ProductTemplateBusinessType_BusinessType]
GO
ALTER TABLE [dbo].[ProductTemplateBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateBusinessType_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateBusinessType] CHECK CONSTRAINT [FK_ProductTemplateBusinessType_ProductTemplate]
GO
ALTER TABLE [dbo].[ProductTemplateCategory]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateCategory_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateCategory] CHECK CONSTRAINT [FK_ProductTemplateCategory_Category]
GO
ALTER TABLE [dbo].[ProductTemplateCategory]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateCategory_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateCategory] CHECK CONSTRAINT [FK_ProductTemplateCategory_ProductTemplate]
GO
ALTER TABLE [dbo].[ProductTemplateMedia]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateMedia_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateMedia] CHECK CONSTRAINT [FK_ProductTemplateMedia_ProductTemplate]
GO
ALTER TABLE [dbo].[ProductTemplateSelectableWeight]  WITH CHECK ADD  CONSTRAINT [FK_ProductTemplateSelectableWeight_ProductTemplate] FOREIGN KEY([ProductTemplateId])
REFERENCES [dbo].[ProductTemplate] ([Id])
GO
ALTER TABLE [dbo].[ProductTemplateSelectableWeight] CHECK CONSTRAINT [FK_ProductTemplateSelectableWeight_ProductTemplate]
GO
ALTER TABLE [dbo].[SyncJob]  WITH CHECK ADD  CONSTRAINT [FK_SyncJob_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[SyncJob] CHECK CONSTRAINT [FK_SyncJob_Account]
GO
ALTER TABLE [dbo].[SyncJob]  WITH CHECK ADD  CONSTRAINT [FK_SyncJob_RequestedBy] FOREIGN KEY([RequestedBy])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[SyncJob] CHECK CONSTRAINT [FK_SyncJob_RequestedBy]
GO
ALTER TABLE [dbo].[SyncJobLog]  WITH CHECK ADD  CONSTRAINT [FK_SyncJobLog_SyncJob] FOREIGN KEY([SyncJobId])
REFERENCES [dbo].[SyncJob] ([Id])
GO
ALTER TABLE [dbo].[SyncJobLog] CHECK CONSTRAINT [FK_SyncJobLog_SyncJob]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_Role] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Role] ([Id])
ON UPDATE CASCADE
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_Role]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_UserStatus] FOREIGN KEY([StatusId])
REFERENCES [dbo].[UserStatus] ([Id])
ON UPDATE CASCADE
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_UserStatus]
GO
ALTER TABLE [dbo].[WizardSession]  WITH CHECK ADD  CONSTRAINT [FK_WizardSession_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[WizardSession] CHECK CONSTRAINT [FK_WizardSession_Account]
GO
ALTER TABLE [dbo].[WizardSession]  WITH CHECK ADD  CONSTRAINT [FK_WizardSession_StartedByUser] FOREIGN KEY([StartedByUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[WizardSession] CHECK CONSTRAINT [FK_WizardSession_StartedByUser]
GO
/****** Object:  StoredProcedure [dbo].[usp_OnboardAccountFromTemplates]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[usp_OnboardAccountFromTemplates]
(
    @AccountId BIGINT,
    @StartedByUserId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    /* 1. Create WizardSession row (step 1 start) */
    INSERT INTO [dbo].[WizardSession] (
        [AccountId],
        [StartedByUserId],
        [Step],
        [Status]
    )
    VALUES
    (@AccountId, @StartedByUserId, 1, N'InProgress');

    /* 2. Clone categories -> AccountCategory
          Each global Category becomes enabled by default, same name, same sort.
          (Later user can rename, reorder, disable.)
    */
    INSERT INTO [dbo].[AccountCategory] (
        [AccountId],
        [CategoryId],
        [ParentAccountCategoryId],
        [CustomName],
        [SortOrder],
        [IsEnabled]
    )
    SELECT
        @AccountId,
        c.[Id],
        NULL,               -- we'll do hierarchy link in a second pass
        NULL,               -- CustomName defaults to null (use global name)
        c.[SortOrder],
        1                   -- default enabled
    FROM [dbo].[Category] c
    WHERE c.[IsActive] = 1;

    /* Rebuild parent-child links at account-level.
       We'll join CategoryHierarchy to map Parent/Child to the newly inserted AccountCategory rows.
       We'll UPDATE after INSERT because we need all AccountCategory rows first.
    */
    ;WITH cat_ac AS (
        SELECT ac.[Id] AS AccountCategoryId, ac.[CategoryId]
        FROM [dbo].[AccountCategory] ac
        WHERE ac.[AccountId] = @AccountId
    )
    UPDATE acChild
    SET acChild.[ParentAccountCategoryId] = acParent.[AccountCategoryId]
    FROM [dbo].[AccountCategory] acChild
    JOIN [dbo].[CategoryHierarchy] ch
         ON ch.[ChildCategoryId] = acChild.[CategoryId]
    JOIN cat_ac acParent
         ON acParent.[CategoryId] = ch.[ParentCategoryId]
    WHERE acChild.[AccountId] = @AccountId;

    /* 3. Clone ProductTemplate into AccountProduct.

       This is where you'll filter later:
       - by Account.IsKosherShop? Then skip templates where IsKosherDefault=0 or KosherStatus=NotKosher.
       - by chosen BusinessTypes (meat/fish/spices) from wizard step 1 (we don't have that yet here).
    */

    INSERT INTO [dbo].[AccountProduct] (
        [AccountId],
        [ProductTemplateId],
        [IsEnabled],
        [EditingStatus],
        [Title],
        [ShortDescription],
        [DescriptionHtml],
        [BrandId],
        [SupplierId],
        [IsKosher],
        [KosherStatusId],
        [WeightPricingModelId],
        [ShowPricePer100g],
        [BaseUnitPrice],
        [BaseUnitId],
        [BaseWeightGrams],
        [StockQuantity],
        [Sku]
    )
    SELECT
        @AccountId,
        pt.[Id],
        1,                       -- default: enabled
        N'NotEdited',
        pt.[Title],
        pt.[ShortDescription],
        pt.[DescriptionHtml],
        pt.[BrandId],
        pt.[SupplierId],
        pt.[IsKosherDefault],
        pt.[KosherStatusId],
        pt.[WeightPricingModelId],
        pt.[ShowPricePer100g],
        pt.[BaseUnitPrice],
        pt.[BaseUnitId],
        pt.[BaseWeightGrams],
        NULL,                    -- initial stock unknown
        pt.[Sku]
    FROM [dbo].[ProductTemplate] pt
    WHERE pt.[IsActive] = 1;

    /* 4. Map products to account categories:
          For each AccountProduct that came from ProductTemplate T,
          add rows in AccountProductCategory corresponding to each Category of T.
    */
    INSERT INTO [dbo].[AccountProductCategory] (
        [AccountProductId],
        [AccountCategoryId]
    )
    SELECT
        ap.[Id] AS AccountProductId,
        ac.[Id] AS AccountCategoryId
    FROM [dbo].[AccountProduct] ap
    JOIN [dbo].[ProductTemplateCategory] ptc
      ON ptc.[ProductTemplateId] = ap.[ProductTemplateId]
    JOIN [dbo].[AccountCategory] ac
      ON ac.[AccountId] = ap.[AccountId]
     AND ac.[CategoryId] = ptc.[CategoryId]
    WHERE ap.[AccountId] = @AccountId;

    /* 5. Copy media (primary image etc.) from template to account product */
    INSERT INTO [dbo].[AccountProductMedia] (
        [AccountProductId],
        [Url],
        [AltText],
        [SortOrder],
        [IsPrimary]
    )
    SELECT
        ap.[Id],
        ptm.[Url],
        ptm.[AltText],
        ptm.[SortOrder],
        ptm.[IsPrimary]
    FROM [dbo].[AccountProduct] ap
    JOIN [dbo].[ProductTemplateMedia] ptm
      ON ptm.[ProductTemplateId] = ap.[ProductTemplateId]
    WHERE ap.[AccountId] = @AccountId;

    /* 6. Copy attributes and allowed options
          - ProductTemplateAttribute -> AccountProductAttribute
          - ProductTemplateAttributeOption -> AccountProductAttributeValue
    */
    ;WITH TemplateAttr AS (
        SELECT
            ap.[Id]                        AS AccountProductId,
            pta.[Id]                       AS ProductTemplateAttributeId,
            pta.[AttributeId],
            pta.[IsVariantAxis]
        FROM [dbo].[AccountProduct] ap
        JOIN [dbo].[ProductTemplateAttribute] pta
          ON pta.[ProductTemplateId] = ap.[ProductTemplateId]
        WHERE ap.[AccountId] = @AccountId
    )
    INSERT INTO [dbo].[AccountProductAttribute] (
        [AccountProductId],
        [AttributeId],
        [IsVariantAxis]
    )
    SELECT
        t.[AccountProductId],
        t.[AttributeId],
        t.[IsVariantAxis]
    FROM TemplateAttr t;

    /* Now copy the options.
       We need to join the newly inserted AccountProductAttribute rows by (AccountProductId, AttributeId),
       and match them back to ProductTemplateAttributeOption.
    */
    INSERT INTO [dbo].[AccountProductAttributeValue] (
        [AccountProductAttributeId],
        [AttributeOptionId],
        [ValueText],
        [SortOrder]
    )
    SELECT
        apa.[Id] AS AccountProductAttributeId,
        ptao.[AttributeOptionId],
        NULL,
        0
    FROM [dbo].[AccountProductAttribute] apa
    JOIN [dbo].[AccountProduct] ap
      ON ap.[Id] = apa.[AccountProductId]
    JOIN [dbo].[ProductTemplateAttribute] pta
      ON pta.[ProductTemplateId] = ap.[ProductTemplateId]
     AND pta.[AttributeId] = apa.[AttributeId]
    JOIN [dbo].[ProductTemplateAttributeOption] ptao
      ON ptao.[ProductTemplateAttributeId] = pta.[Id]
    WHERE ap.[AccountId] = @AccountId;

    /* 7. Pre-build AccountProductVariant from ProductTemplateSelectableWeight,
          so the butcher immediately sees 300g / 500g etc as variant rows.
          We don't yet set AttributeOption links. That could be a later enhancement.
    */
    INSERT INTO [dbo].[AccountProductVariant] (
        [AccountProductId],
        [VariantSku],
        [VariantTitle],
        [Price],
        [StockQuantity],
        [WeightGrams],
        [IsEnabled],
        [SortOrder]
    )
    SELECT
        ap.[Id],
        CONCAT(pt.[Sku], N'-', REPLACE(psw.[Label], N' ', N'')) AS VariantSku,
        psw.[Label] AS VariantTitle,
        NULL AS Price,           -- butcher will fill
        NULL AS StockQuantity,   -- butcher will fill
        psw.[WeightGrams],
        1 AS IsEnabled,
        psw.[SortOrder]
    FROM [dbo].[AccountProduct] ap
    JOIN [dbo].[ProductTemplateSelectableWeight] psw
      ON psw.[ProductTemplateId] = ap.[ProductTemplateId]
    JOIN [dbo].[ProductTemplate] pt
      ON pt.[Id] = ap.[ProductTemplateId]
    WHERE ap.[AccountId] = @AccountId;

    /* 8. Log "NotEdited" state in ProductEditLog for auditing */
    INSERT INTO [dbo].[ProductEditLog] (
        [AccountProductId],
        [UserId],
        [FromStatus],
        [ToStatus],
        [Notes]
    )
    SELECT
        ap.[Id],
        @StartedByUserId,
        N'',
        N'NotEdited',
        N'initial onboarding from templates'
    FROM [dbo].[AccountProduct] ap
    WHERE ap.[AccountId] = @AccountId;

END
GO
/****** Object:  StoredProcedure [dbo].[usp_PushAccountProductsToWooCommerce]    Script Date: 10/25/2025 2:14:45 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[usp_PushAccountProductsToWooCommerce]
(
    @AccountId BIGINT,
    @RequestedBy INT
)
AS
BEGIN
    SET NOCOUNT ON;

    /* 0. Create SyncJob row in Pending status for traceability */
    INSERT INTO [dbo].[SyncJob] (
        [AccountId],
        [JobType],
        [Status],
        [RequestedBy],
        [Message]
    )
    VALUES
    (@AccountId, N'PushProducts', N'Pending', @RequestedBy, N'Preparing product payload for WooCommerce');

    DECLARE @SyncJobId BIGINT = SCOPE_IDENTITY();

    /* 1. Temp table of products that should be published:
        - Enabled
        - EditingStatus = 'Published'
        - Not already mapped in EcomProductMap
    */
    IF OBJECT_ID('tempdb..#ToPublish') IS NOT NULL DROP TABLE #ToPublish;

    CREATE TABLE #ToPublish (
        AccountProductId BIGINT PRIMARY KEY,
        Sku              NVARCHAR(100),
        Title            NVARCHAR(300),
        DescriptionHtml  NVARCHAR(MAX),
        Price            DECIMAL(18,2),
        StockQuantity    DECIMAL(18,3),
        WeightGrams      DECIMAL(18,3),
        CategoryNames    NVARCHAR(MAX),
        PrimaryImageUrl  NVARCHAR(1000)
    );

    ;WITH catmap AS (
        SELECT
            apc.[AccountProductId],
            STUFF((
                SELECT N', ' + COALESCE(ac.[CustomName], c.[Name])
                FROM [dbo].[AccountProductCategory] apc2
                JOIN [dbo].[AccountCategory] ac
                  ON ac.[Id] = apc2.[AccountCategoryId]
                JOIN [dbo].[Category] c
                  ON c.[Id] = ac.[CategoryId]
                WHERE apc2.[AccountProductId] = apc.[AccountProductId]
                  AND ac.[IsEnabled] = 1
                FOR XML PATH(''), TYPE
            ).value('.', 'nvarchar(max)'), 1, 2, N'') AS [CategoryNames]
        FROM [dbo].[AccountProductCategory] apc
        GROUP BY apc.[AccountProductId]
    ),
    primary_image AS (
        SELECT
            apm.[AccountProductId],
            apm.[Url] AS [PrimaryImageUrl],
            ROW_NUMBER() OVER (PARTITION BY apm.[AccountProductId] ORDER BY apm.[IsPrimary] DESC, apm.[SortOrder]) AS rn
        FROM [dbo].[AccountProductMedia] apm
    ),
    first_image AS (
        SELECT [AccountProductId], [PrimaryImageUrl]
        FROM primary_image
        WHERE rn = 1
    ),
    first_variant AS (
        -- We'll just grab first enabled variant for price/weight defaults
        SELECT
            apv.[AccountProductId],
            apv.[Price],
            apv.[StockQuantity],
            apv.[WeightGrams],
            ROW_NUMBER() OVER (PARTITION BY apv.[AccountProductId] ORDER BY apv.[SortOrder]) AS rn
        FROM [dbo].[AccountProductVariant] apv
        WHERE apv.[IsEnabled] = 1
    ),
    fv AS (
        SELECT [AccountProductId],[Price],[StockQuantity],[WeightGrams]
        FROM first_variant
        WHERE rn = 1
    )
    INSERT INTO #ToPublish (
        AccountProductId, Sku, Title, DescriptionHtml,
        Price, StockQuantity, WeightGrams,
        CategoryNames, PrimaryImageUrl
    )
    SELECT
        ap.[Id],
        ap.[Sku],
        COALESCE(ap.[Title], pt.[Title]) AS Title,
        COALESCE(ap.[DescriptionHtml], pt.[DescriptionHtml]) AS DescriptionHtml,
        fv.[Price],
        fv.[StockQuantity],
        fv.[WeightGrams],
        catmap.[CategoryNames],
        fi.[PrimaryImageUrl]
    FROM [dbo].[AccountProduct] ap
    JOIN [dbo].[ProductTemplate] pt
      ON pt.[Id] = ap.[ProductTemplateId]
    LEFT JOIN catmap
      ON catmap.[AccountProductId] = ap.[Id]
    LEFT JOIN fi
      ON fi.[AccountProductId] = ap.[Id]
    LEFT JOIN fv
      ON fv.[AccountProductId] = ap.[Id]
    WHERE ap.[AccountId] = @AccountId
      AND ap.[IsEnabled] = 1
      AND ap.[EditingStatus] = N'Published'
      AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[EcomProductMap] m
            WHERE m.[AccountProductId] = ap.[Id]
      );

    /* 2. Log how many products are going out */
    INSERT INTO [dbo].[SyncJobLog] (
        [SyncJobId],
        [Level],
        [Message]
    )
    SELECT
        @SyncJobId,
        N'Info',
        CONCAT(N'Prepared ', COUNT(1), N' products for publish')
    FROM #ToPublish;

    /* 3. Return rows to caller (C# layer will call Woo API using this data)
          NOTE: This proc does not INSERT into EcomProductMap / EcomVariantMap.
          That should happen AFTER Woo says "OK, product created with ID X".
    */
    SELECT
        t.*,
        @SyncJobId AS SyncJobId
    FROM #ToPublish t;

END;
GO
