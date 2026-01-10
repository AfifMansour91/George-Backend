GO
/****** Object:  Table [dbo].[Account]    Script Date: 10/01/2026 20:28:17 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Account](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](2000) NULL,
	[Address] [nvarchar](500) NULL,
	[City] [nvarchar](200) NULL,
	[State] [nvarchar](200) NULL,
	[Zip] [nvarchar](50) NULL,
	[Phone] [nvarchar](50) NULL,
	[ManagerId] [int] NULL,
	[ManagerName] [nvarchar](200) NULL,
	[ManagerEmail] [nvarchar](250) NOT NULL,
	[StatusId] [int] NULL,
	[WizardStatusId] [int] NULL,
	[WizardTypeId] [int] NULL,
	[WizardStep] [int] NULL,
	[ContentOwnerId] [int] NULL,
	[LogoUrl] [nvarchar](1000) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_Account] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AccountStatus]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AccountStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Attribute]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Attribute](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[SiteId] [int] NOT NULL,
 CONSTRAINT [PK_Attribute] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AttributeValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttributeValue](
	[AttributeId] [int] NOT NULL,
	[Value] [nvarchar](200) NOT NULL,
 CONSTRAINT [PK_AttributeValue] PRIMARY KEY CLUSTERED 
(
	[AttributeId] ASC,
	[Value] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Brand]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Brand](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[AccountId] [int] NULL,
 CONSTRAINT [PK_Brand] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](2000) NULL,
	[Icon] [nvarchar](200) NULL,
 CONSTRAINT [PK_BusinessType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessTypeCategory]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessTypeCategory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[BusinessTypeId] [int] NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
 CONSTRAINT [PK_BusinessTypeCategory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Category]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Category](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[ParentCategoryId] [int] NULL,
	[Description] [nvarchar](2000) NULL,
	[CustomName] [nvarchar](200) NULL,
	[IsEnabled] [bit] NULL,
	[SortOrder] [int] NULL,
	[DisplayAsMain] [bit] NULL,
	[AccountId] [int] NULL,
	[SourceGlobalCategoryId] [int] NULL,
	[WooCommerceId] [int] NULL,
 CONSTRAINT [PK_Category] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CategorySite]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CategorySite](
	[CategoryId] [int] NOT NULL,
	[SiteId] [int] NOT NULL,
 CONSTRAINT [PK_CategorySite] PRIMARY KEY CLUSTERED 
(
	[CategoryId] ASC,
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContentOwner]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContentOwner](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ContentOwner] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GlobalCategory]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GlobalCategory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](2000) NULL,
	[ParentGlobalCategoryId] [int] NULL,
	[SortOrder] [int] NULL,
	[ProductCount] [int] NULL,
 CONSTRAINT [PK_GlobalCategory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GlobalCategoryBusinessType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GlobalCategoryBusinessType](
	[GlobalCategoryId] [int] NOT NULL,
	[BusinessTypeId] [int] NOT NULL,
 CONSTRAINT [PK_GlobalCategoryBusinessType] PRIMARY KEY CLUSTERED 
(
	[GlobalCategoryId] ASC,
	[BusinessTypeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Media]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Media](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Url] [nvarchar](1000) NOT NULL,
	[Name] [nvarchar](300) NOT NULL,
	[TypeId] [int] NULL,
	[BusinessTypeId] [int] NULL,
	[FileSize] [bigint] NULL,
	[UsageCount] [int] NULL,
	[AccountId] [int] NULL,
 CONSTRAINT [PK_Media] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MediaCategory]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MediaCategory](
	[MediaId] [int] NOT NULL,
	[CategoryId] [int] NOT NULL,
 CONSTRAINT [PK_MediaCategory] PRIMARY KEY CLUSTERED 
(
	[MediaId] ASC,
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MediaTag]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MediaTag](
	[MediaId] [int] NOT NULL,
	[TagId] [int] NOT NULL,
 CONSTRAINT [PK_MediaTag] PRIMARY KEY CLUSTERED 
(
	[MediaId] ASC,
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MediaType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MediaType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_MediaType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Product]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Product](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[Name] [nvarchar](300) NOT NULL,
	[ShortDescription] [nvarchar](2000) NULL,
	[LongDescription] [nvarchar](max) NULL,
	[Price] [decimal](18, 2) NULL,
	[SalePrice] [decimal](18, 2) NULL,
	[SalePriceStartDate] [datetime2](0) NULL,
	[SalePriceEndDate] [datetime2](0) NULL,
	[CostPrice] [decimal](18, 2) NULL,
	[Sku] [nvarchar](100) NULL,
	[StockManagementTypeId] [int] NULL,
	[StockQuantity] [int] NULL,
	[StockStatusId] [int] NULL,
	[Weight] [decimal](18, 4) NULL,
	[ShippingClassId] [int] NULL,
	[StatusId] [int] NULL,
	[VisibilityId] [int] NULL,
	[SetupTypeId] [int] NULL,
	[BrandId] [int] NULL,
	[SupplierId] [int] NULL,
	[IsKosher] [bit] NULL,
	[IsWeighted] [bit] NULL,
	[WeightConfigId] [int] NULL,
	[SeoTitle] [nvarchar](300) NULL,
	[SeoDescription] [nvarchar](2000) NULL,
	[TemplateId] [nvarchar](100) NULL,
	[SourceProductId] [nvarchar](100) NULL,
	[AccountId] [int] NULL,
	[WooCommerceId] [int] NULL,
 CONSTRAINT [PK_Product] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductCategory]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductCategory](
	[ProductId] [int] NOT NULL,
	[CategoryId] [int] NOT NULL,
	[IsPrimary] [bit] NOT NULL,
 CONSTRAINT [PK_ProductCategory] PRIMARY KEY CLUSTERED 
(
	[ProductId] ASC,
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductImage]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductImage](
	[ProductId] [int] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[Url] [nvarchar](1000) NOT NULL,
 CONSTRAINT [PK_ProductImage] PRIMARY KEY CLUSTERED 
(
	[ProductId] ASC,
	[Url] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductOption]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductOption](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ProductOption] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductOptionValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductOptionValue](
	[ProductOptionId] [int] NOT NULL,
	[Value] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_ProductOptionValue] PRIMARY KEY CLUSTERED 
(
	[ProductOptionId] ASC,
	[Value] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductSite]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductSite](
	[ProductId] [int] NOT NULL,
	[SiteId] [int] NOT NULL,
 CONSTRAINT [PK_ProductSite] PRIMARY KEY CLUSTERED 
(
	[ProductId] ASC,
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductStatus]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ProductStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductTag]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductTag](
	[ProductId] [int] NOT NULL,
	[TagId] [int] NOT NULL,
 CONSTRAINT [PK_ProductTag] PRIMARY KEY CLUSTERED 
(
	[ProductId] ASC,
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductVariant]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductVariant](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[ImageUrl] [nvarchar](1000) NULL,
	[Price] [decimal](18, 2) NULL,
	[SalePrice] [decimal](18, 2) NULL,
	[StockQuantity] [int] NULL,
	[Sku] [nvarchar](100) NULL,
	[Weight] [decimal](18, 4) NULL,
	[IsDeleted] [bit] NOT NULL,
	[WooCommerceVariationId] [int] NULL,
 CONSTRAINT [PK_ProductVariant] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductVariantOptionValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductVariantOptionValue](
	[ProductVariantId] [int] NOT NULL,
	[OptionName] [nvarchar](100) NOT NULL,
	[OptionValue] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_ProductVariantOptionValue] PRIMARY KEY CLUSTERED 
(
	[ProductVariantId] ASC,
	[OptionName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Role]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Role](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SetupType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SetupType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](40) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_SetupType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ShippingClass]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ShippingClass](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ShippingClass] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Site]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Site](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[AccountId] [int] NOT NULL,
	[SiteName] [nvarchar](200) NOT NULL,
	[Location] [nvarchar](500) NULL,
	[Description] [nvarchar](2000) NULL,
	[Status] [nvarchar](20) NULL,
	[ContactEmail] [nvarchar](250) NULL,
	[ContactPhone] [nvarchar](50) NULL,
	[IsKosherSite] [bit] NULL,
	[AllowWeightedProducts] [bit] NULL,
	[Currency] [nvarchar](10) NOT NULL,
	[WooCommerceUrl] [nvarchar](500) NULL,
	[WooCommerceKey] [nvarchar](250) NULL,
	[WooCommerceSecret] [nvarchar](250) NULL,
 CONSTRAINT [PK_Site] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SiteBusinessType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SiteBusinessType](
	[SiteId] [int] NOT NULL,
	[BusinessTypeId] [int] NOT NULL,
 CONSTRAINT [PK_SiteBusinessType] PRIMARY KEY CLUSTERED 
(
	[SiteId] ASC,
	[BusinessTypeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SiteUser]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SiteUser](
	[SiteId] [int] NOT NULL,
	[UserId] [int] NOT NULL,
 CONSTRAINT [PK_SiteUser] PRIMARY KEY CLUSTERED 
(
	[SiteId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockManagementType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockManagementType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_StockManagementType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockStatus]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_StockStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Supplier]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Supplier](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[AccountId] [int] NULL,
 CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SystemConfiguration]    Script Date: 10/01/2026 20:28:18 ******/
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
/****** Object:  Table [dbo].[Tag]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Tag](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
	[AccountId] [int] NULL,
 CONSTRAINT [PK_Tag] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateAttribute]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateAttribute](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[Name] [nvarchar](200) NOT NULL,
 CONSTRAINT [PK_TemplateAttribute] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateAttributeSite]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateAttributeSite](
	[TemplateAttributeId] [int] NOT NULL,
	[SiteId] [int] NOT NULL,
 CONSTRAINT [PK_TemplateAttributeSite] PRIMARY KEY CLUSTERED 
(
	[TemplateAttributeId] ASC,
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateAttributeValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateAttributeValue](
	[TemplateAttributeId] [int] NOT NULL,
	[Value] [nvarchar](200) NOT NULL,
 CONSTRAINT [PK_TemplateAttributeValue] PRIMARY KEY CLUSTERED 
(
	[TemplateAttributeId] ASC,
	[Value] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProduct]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProduct](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[TemplateId] [nvarchar](100) NULL,
	[Name] [nvarchar](300) NOT NULL,
	[ShortDescription] [nvarchar](2000) NULL,
	[LongDescription] [nvarchar](max) NULL,
	[Price] [decimal](18, 2) NULL,
	[SalePrice] [decimal](18, 2) NULL,
	[SalePriceStartDate] [datetime2](0) NULL,
	[SalePriceEndDate] [datetime2](0) NULL,
	[CostPrice] [decimal](18, 2) NULL,
	[Sku] [nvarchar](100) NULL,
	[StockManagementTypeId] [int] NULL,
	[StockQuantity] [int] NULL,
	[StockStatusId] [int] NULL,
	[Weight] [decimal](18, 4) NULL,
	[ShippingClassId] [int] NULL,
	[StatusId] [int] NULL,
	[VisibilityId] [int] NULL,
	[BrandId] [int] NULL,
	[SupplierId] [int] NULL,
	[IsKosher] [bit] NULL,
	[IsWeighted] [bit] NULL,
	[SetupTypeId] [int] NULL,
	[WeightConfigId] [int] NULL,
	[SeoTitle] [nvarchar](300) NULL,
	[SeoDescription] [nvarchar](2000) NULL,
	[SourceProductId] [nvarchar](100) NULL,
 CONSTRAINT [PK_TemplateProduct] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductCategory]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductCategory](
	[TemplateProductId] [int] NOT NULL,
	[IsPrimary] [bit] NOT NULL,
	[GlobalCategoryId] [int] NOT NULL,
 CONSTRAINT [PK_TemplateProductCategory] PRIMARY KEY CLUSTERED 
(
	[TemplateProductId] ASC,
	[GlobalCategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductImage]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductImage](
	[TemplateProductId] [int] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[Url] [nvarchar](1000) NOT NULL,
 CONSTRAINT [PK_TemplateProductImage] PRIMARY KEY CLUSTERED 
(
	[TemplateProductId] ASC,
	[Url] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductOption]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductOption](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TemplateProductId] [int] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_TemplateProductOption] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductOptionValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductOptionValue](
	[TemplateProductOptionId] [int] NOT NULL,
	[Value] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_TemplateProductOptionValue] PRIMARY KEY CLUSTERED 
(
	[TemplateProductOptionId] ASC,
	[Value] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductSite]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductSite](
	[TemplateProductId] [int] NOT NULL,
	[SiteId] [int] NOT NULL,
 CONSTRAINT [PK_TemplateProductSite] PRIMARY KEY CLUSTERED 
(
	[TemplateProductId] ASC,
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductTag]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductTag](
	[TemplateProductId] [int] NOT NULL,
	[TagId] [int] NOT NULL,
 CONSTRAINT [PK_TemplateProductTag] PRIMARY KEY CLUSTERED 
(
	[TemplateProductId] ASC,
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductVariant]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductVariant](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TemplateProductId] [int] NOT NULL,
	[ImageUrl] [nvarchar](1000) NULL,
	[Price] [decimal](18, 2) NULL,
	[SalePrice] [decimal](18, 2) NULL,
	[StockQuantity] [int] NULL,
	[Sku] [nvarchar](100) NULL,
	[Weight] [decimal](18, 4) NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_TemplateProductVariant] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemplateProductVariantOptionValue]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateProductVariantOptionValue](
	[TemplateProductVariantId] [int] NOT NULL,
	[OptionName] [nvarchar](100) NOT NULL,
	[OptionValue] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_TemplateProductVariantOptionValue] PRIMARY KEY CLUSTERED 
(
	[TemplateProductVariantId] ASC,
	[OptionName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Unit]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Unit](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Unit] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UnitWeightMode]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UnitWeightMode](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UnitWeightMode] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[User]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[User](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[GuidId] [uniqueidentifier] NOT NULL,
	[CreationTime] [datetime2](0) NOT NULL,
	[UpdatedDate] [datetime2](0) NULL,
	[CreationUserId] [int] NULL,
	[UpdateUserId] [int] NULL,
	[RoleId] [int] NOT NULL,
	[AccountId] [int] NULL,
	[StatusId] [int] NOT NULL,
	[FirstName] [nvarchar](50) NOT NULL,
	[LastName] [nvarchar](50) NOT NULL,
	[FullName]  AS (([FirstName]+N' ')+[LastName]) PERSISTED NOT NULL,
	[Email] [nvarchar](250) NULL,
	[IsEmailVerified] [bit] NOT NULL,
	[Password] [nvarchar](250) NULL,
	[Otp] [nvarchar](50) NULL,
	[LastLoginDate] [datetime2](0) NULL,
	[LockoutFailCount] [int] NOT NULL,
	[LockoutExpiration] [datetime2](0) NULL,
	[RefreshToken] [nvarchar](250) NULL,
	[RefreshTokenExpiration] [datetime2](0) NULL,
	[Phone] [nvarchar](50) NULL,
	[AvatarUrl] [nvarchar](500) NULL,
	[Notes] [nvarchar](max) NULL,
	[OtpExpiration] [datetime2](0) NULL,
 CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserStatus]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Visibility]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Visibility](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Visibility] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WeightConfig]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WeightConfig](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[UnitId] [int] NULL,
	[StartWeight] [nvarchar](50) NULL,
	[Step] [nvarchar](50) NULL,
	[FixedWeightPerUnit] [bit] NULL,
	[UnitWeight] [nvarchar](50) NULL,
	[UnitWeightModeId] [int] NULL,
	[WeightOptions] [nvarchar](2000) NULL,
	[WeightByVariant] [bit] NULL,
	[ShowPricePer100g] [bit] NULL,
 CONSTRAINT [PK_WeightConfig] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WizardStatus]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WizardStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_WizardStatus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WizardType]    Script Date: 10/01/2026 20:28:18 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WizardType](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](30) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_WizardType] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Attribute_SiteId_Name_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Attribute_SiteId_Name_NotDeleted] ON [dbo].[Attribute]
(
	[SiteId] ASC,
	[Name] ASC
)
WHERE ([IsDeleted]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Brand_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Brand_AccountId] ON [dbo].[Brand]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Brand_AccountId_Name_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Brand_AccountId_Name_NotDeleted] ON [dbo].[Brand]
(
	[AccountId] ASC,
	[Name] ASC
)
WHERE ([IsDeleted]=(0) AND [AccountId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Category_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Category_AccountId] ON [dbo].[Category]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Category_SourceGlobalCategoryId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Category_SourceGlobalCategoryId] ON [dbo].[Category]
(
	[SourceGlobalCategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Category_Account_Parent_Name_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Category_Account_Parent_Name_NotDeleted] ON [dbo].[Category]
(
	[AccountId] ASC,
	[ParentCategoryId] ASC,
	[Name] ASC
)
WHERE ([IsDeleted]=(0) AND [AccountId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_CategorySite_SiteId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_CategorySite_SiteId] ON [dbo].[CategorySite]
(
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Media_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Media_AccountId] ON [dbo].[Media]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_MediaTag_TagId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_MediaTag_TagId] ON [dbo].[MediaTag]
(
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Product_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Product_AccountId] ON [dbo].[Product]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Product_AccountId_Sku_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Product_AccountId_Sku_NotDeleted] ON [dbo].[Product]
(
	[AccountId] ASC,
	[Sku] ASC
)
WHERE ([IsDeleted]=(0) AND [Sku] IS NOT NULL AND [AccountId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ProductCategory_CategoryId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_ProductCategory_CategoryId] ON [dbo].[ProductCategory]
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ProductSite_SiteId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_ProductSite_SiteId] ON [dbo].[ProductSite]
(
	[SiteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ProductTag_TagId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_ProductTag_TagId] ON [dbo].[ProductTag]
(
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Supplier_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Supplier_AccountId] ON [dbo].[Supplier]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Supplier_AccountId_Name_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Supplier_AccountId_Name_NotDeleted] ON [dbo].[Supplier]
(
	[AccountId] ASC,
	[Name] ASC
)
WHERE ([IsDeleted]=(0) AND [AccountId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tag_AccountId]    Script Date: 10/01/2026 20:28:18 ******/
CREATE NONCLUSTERED INDEX [IX_Tag_AccountId] ON [dbo].[Tag]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Tag_AccountId_Name_NotDeleted]    Script Date: 10/01/2026 20:28:18 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Tag_AccountId_Name_NotDeleted] ON [dbo].[Tag]
(
	[AccountId] ASC,
	[Name] ASC
)
WHERE ([IsDeleted]=(0) AND [AccountId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_ContentOwnerId]  DEFAULT ((1)) FOR [ContentOwnerId]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_StatusText]  DEFAULT ('Active') FOR [Status]
GO
ALTER TABLE [dbo].[Account] ADD  CONSTRAINT [DF_Account_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[AccountStatus] ADD  CONSTRAINT [DF_AccountStatus_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Attribute] ADD  CONSTRAINT [DF_Attribute_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Attribute] ADD  CONSTRAINT [DF_Attribute_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[Attribute] ADD  CONSTRAINT [DF_Attribute_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Brand] ADD  CONSTRAINT [DF_Brand_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Brand] ADD  CONSTRAINT [DF_Brand_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[BusinessType] ADD  CONSTRAINT [DF_BusinessType_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[BusinessType] ADD  CONSTRAINT [DF_BusinessType_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[BusinessType] ADD  CONSTRAINT [DF_BusinessType_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[BusinessTypeCategory] ADD  CONSTRAINT [DF_BusinessTypeCategory_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Category] ADD  CONSTRAINT [DF_Category_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ContentOwner] ADD  CONSTRAINT [DF_ContentOwner_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[GlobalCategory] ADD  CONSTRAINT [DF_GlobalCategory_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[GlobalCategory] ADD  CONSTRAINT [DF_GlobalCategory_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[GlobalCategory] ADD  CONSTRAINT [DF_GlobalCategory_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Media] ADD  CONSTRAINT [DF_Media_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Media] ADD  CONSTRAINT [DF_Media_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[MediaType] ADD  CONSTRAINT [DF_MediaType_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Product] ADD  CONSTRAINT [DF_Product_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Product] ADD  CONSTRAINT [DF_Product_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[Product] ADD  CONSTRAINT [DF_Product_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Product] ADD  CONSTRAINT [DF_Product_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ProductCategory] ADD  CONSTRAINT [DF_ProductCategory_IsPrimary]  DEFAULT ((0)) FOR [IsPrimary]
GO
ALTER TABLE [dbo].[ProductImage] ADD  CONSTRAINT [DF_ProductImage_Sort]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[ProductOption] ADD  CONSTRAINT [DF_ProductOption_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ProductStatus] ADD  CONSTRAINT [DF_ProductStatus_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ProductVariant] ADD  CONSTRAINT [DF_ProductVariant_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Role] ADD  CONSTRAINT [DF_Role_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[SetupType] ADD  CONSTRAINT [DF_SetupType_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ShippingClass] ADD  CONSTRAINT [DF_ShippingClass_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Site] ADD  CONSTRAINT [DF_Site_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Site] ADD  CONSTRAINT [DF_Site_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[Site] ADD  CONSTRAINT [DF_Site_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Site] ADD  CONSTRAINT [DF_Site_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Site] ADD  CONSTRAINT [DF_Site_Currency]  DEFAULT ('ILS') FOR [Currency]
GO
ALTER TABLE [dbo].[StockManagementType] ADD  CONSTRAINT [DF_StockManagementType_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[StockStatus] ADD  CONSTRAINT [DF_StockStatus_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[Tag] ADD  CONSTRAINT [DF_Tag_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Tag] ADD  CONSTRAINT [DF_Tag_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[TemplateAttribute] ADD  CONSTRAINT [DF_TemplateAttribute_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[TemplateAttribute] ADD  CONSTRAINT [DF_TemplateAttribute_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[TemplateAttribute] ADD  CONSTRAINT [DF_TemplateAttribute_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[TemplateProduct] ADD  CONSTRAINT [DF_TemplateProduct_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[TemplateProduct] ADD  CONSTRAINT [DF_TemplateProduct_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[TemplateProduct] ADD  CONSTRAINT [DF_TemplateProduct_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[TemplateProductCategory] ADD  CONSTRAINT [DF_TemplateProductCategory_IsPrimary]  DEFAULT ((0)) FOR [IsPrimary]
GO
ALTER TABLE [dbo].[TemplateProductImage] ADD  CONSTRAINT [DF_TemplateProductImage_Sort]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[TemplateProductOption] ADD  CONSTRAINT [DF_TemplateProductOption_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[TemplateProductVariant] ADD  CONSTRAINT [DF_TemplateProductVariant_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Unit] ADD  CONSTRAINT [DF_Unit_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UnitWeightMode] ADD  CONSTRAINT [DF_UnitWeightMode_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_Guid]  DEFAULT (newid()) FOR [GuidId]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_CreationTime]  DEFAULT (sysutcdatetime()) FOR [CreationTime]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsEmailVerified]  DEFAULT ((0)) FOR [IsEmailVerified]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_LockoutFailCount]  DEFAULT ((0)) FOR [LockoutFailCount]
GO
ALTER TABLE [dbo].[UserStatus] ADD  CONSTRAINT [DF_UserStatus_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Visibility] ADD  CONSTRAINT [DF_Visibility_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[WeightConfig] ADD  CONSTRAINT [DF_WeightConfig_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[WizardStatus] ADD  CONSTRAINT [DF_WizardStatus_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[WizardType] ADD  CONSTRAINT [DF_WizardType_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_ContentOwner] FOREIGN KEY([ContentOwnerId])
REFERENCES [dbo].[ContentOwner] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_ContentOwner]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_CreationUser]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_Manager] FOREIGN KEY([ManagerId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_Manager]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_Status] FOREIGN KEY([StatusId])
REFERENCES [dbo].[AccountStatus] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_Status]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_UpdateUser]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_WizardStatus] FOREIGN KEY([WizardStatusId])
REFERENCES [dbo].[WizardStatus] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_WizardStatus]
GO
ALTER TABLE [dbo].[Account]  WITH CHECK ADD  CONSTRAINT [FK_Account_WizardType] FOREIGN KEY([WizardTypeId])
REFERENCES [dbo].[WizardType] ([Id])
GO
ALTER TABLE [dbo].[Account] CHECK CONSTRAINT [FK_Account_WizardType]
GO
ALTER TABLE [dbo].[Attribute]  WITH CHECK ADD  CONSTRAINT [FK_Attribute_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Attribute] CHECK CONSTRAINT [FK_Attribute_CreationUser]
GO
ALTER TABLE [dbo].[Attribute]  WITH CHECK ADD  CONSTRAINT [FK_Attribute_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[Attribute] CHECK CONSTRAINT [FK_Attribute_Site]
GO
ALTER TABLE [dbo].[Attribute]  WITH CHECK ADD  CONSTRAINT [FK_Attribute_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Attribute] CHECK CONSTRAINT [FK_Attribute_UpdateUser]
GO
ALTER TABLE [dbo].[AttributeValue]  WITH CHECK ADD  CONSTRAINT [FK_AttributeValue_Attribute] FOREIGN KEY([AttributeId])
REFERENCES [dbo].[Attribute] ([Id])
GO
ALTER TABLE [dbo].[AttributeValue] CHECK CONSTRAINT [FK_AttributeValue_Attribute]
GO
ALTER TABLE [dbo].[Brand]  WITH CHECK ADD  CONSTRAINT [FK_Brand_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Brand] CHECK CONSTRAINT [FK_Brand_Account]
GO
ALTER TABLE [dbo].[Brand]  WITH CHECK ADD  CONSTRAINT [FK_Brand_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Brand] CHECK CONSTRAINT [FK_Brand_CreationUser]
GO
ALTER TABLE [dbo].[Brand]  WITH CHECK ADD  CONSTRAINT [FK_Brand_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Brand] CHECK CONSTRAINT [FK_Brand_UpdateUser]
GO
ALTER TABLE [dbo].[BusinessType]  WITH CHECK ADD  CONSTRAINT [FK_BusinessType_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[BusinessType] CHECK CONSTRAINT [FK_BusinessType_CreationUser]
GO
ALTER TABLE [dbo].[BusinessType]  WITH CHECK ADD  CONSTRAINT [FK_BusinessType_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[BusinessType] CHECK CONSTRAINT [FK_BusinessType_UpdateUser]
GO
ALTER TABLE [dbo].[BusinessTypeCategory]  WITH CHECK ADD  CONSTRAINT [FK_BTC_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[BusinessTypeCategory] CHECK CONSTRAINT [FK_BTC_BusinessType]
GO
ALTER TABLE [dbo].[Category]  WITH CHECK ADD  CONSTRAINT [FK_Category_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Category] CHECK CONSTRAINT [FK_Category_Account]
GO
ALTER TABLE [dbo].[Category]  WITH CHECK ADD  CONSTRAINT [FK_Category_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Category] CHECK CONSTRAINT [FK_Category_CreationUser]
GO
ALTER TABLE [dbo].[Category]  WITH CHECK ADD  CONSTRAINT [FK_Category_Parent] FOREIGN KEY([ParentCategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[Category] CHECK CONSTRAINT [FK_Category_Parent]
GO
ALTER TABLE [dbo].[Category]  WITH CHECK ADD  CONSTRAINT [FK_Category_SourceGlobalCategory] FOREIGN KEY([SourceGlobalCategoryId])
REFERENCES [dbo].[GlobalCategory] ([Id])
GO
ALTER TABLE [dbo].[Category] CHECK CONSTRAINT [FK_Category_SourceGlobalCategory]
GO
ALTER TABLE [dbo].[Category]  WITH CHECK ADD  CONSTRAINT [FK_Category_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Category] CHECK CONSTRAINT [FK_Category_UpdateUser]
GO
ALTER TABLE [dbo].[CategorySite]  WITH CHECK ADD  CONSTRAINT [FK_CategorySite_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[CategorySite] CHECK CONSTRAINT [FK_CategorySite_Category]
GO
ALTER TABLE [dbo].[CategorySite]  WITH CHECK ADD  CONSTRAINT [FK_CategorySite_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[CategorySite] CHECK CONSTRAINT [FK_CategorySite_Site]
GO
ALTER TABLE [dbo].[GlobalCategory]  WITH CHECK ADD  CONSTRAINT [FK_GlobalCategory_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[GlobalCategory] CHECK CONSTRAINT [FK_GlobalCategory_CreationUser]
GO
ALTER TABLE [dbo].[GlobalCategory]  WITH CHECK ADD  CONSTRAINT [FK_GlobalCategory_Parent] FOREIGN KEY([ParentGlobalCategoryId])
REFERENCES [dbo].[GlobalCategory] ([Id])
GO
ALTER TABLE [dbo].[GlobalCategory] CHECK CONSTRAINT [FK_GlobalCategory_Parent]
GO
ALTER TABLE [dbo].[GlobalCategory]  WITH CHECK ADD  CONSTRAINT [FK_GlobalCategory_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[GlobalCategory] CHECK CONSTRAINT [FK_GlobalCategory_UpdateUser]
GO
ALTER TABLE [dbo].[GlobalCategoryBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_GCBT_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[GlobalCategoryBusinessType] CHECK CONSTRAINT [FK_GCBT_BusinessType]
GO
ALTER TABLE [dbo].[GlobalCategoryBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_GCBT_GlobalCategory] FOREIGN KEY([GlobalCategoryId])
REFERENCES [dbo].[GlobalCategory] ([Id])
GO
ALTER TABLE [dbo].[GlobalCategoryBusinessType] CHECK CONSTRAINT [FK_GCBT_GlobalCategory]
GO
ALTER TABLE [dbo].[Media]  WITH CHECK ADD  CONSTRAINT [FK_Media_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Media] CHECK CONSTRAINT [FK_Media_Account]
GO
ALTER TABLE [dbo].[Media]  WITH CHECK ADD  CONSTRAINT [FK_Media_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[Media] CHECK CONSTRAINT [FK_Media_BusinessType]
GO
ALTER TABLE [dbo].[Media]  WITH CHECK ADD  CONSTRAINT [FK_Media_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Media] CHECK CONSTRAINT [FK_Media_CreationUser]
GO
ALTER TABLE [dbo].[Media]  WITH CHECK ADD  CONSTRAINT [FK_Media_Type] FOREIGN KEY([TypeId])
REFERENCES [dbo].[MediaType] ([Id])
GO
ALTER TABLE [dbo].[Media] CHECK CONSTRAINT [FK_Media_Type]
GO
ALTER TABLE [dbo].[Media]  WITH CHECK ADD  CONSTRAINT [FK_Media_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Media] CHECK CONSTRAINT [FK_Media_UpdateUser]
GO
ALTER TABLE [dbo].[MediaCategory]  WITH CHECK ADD  CONSTRAINT [FK_MediaCategory_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[MediaCategory] CHECK CONSTRAINT [FK_MediaCategory_Category]
GO
ALTER TABLE [dbo].[MediaCategory]  WITH CHECK ADD  CONSTRAINT [FK_MediaCategory_Media] FOREIGN KEY([MediaId])
REFERENCES [dbo].[Media] ([Id])
GO
ALTER TABLE [dbo].[MediaCategory] CHECK CONSTRAINT [FK_MediaCategory_Media]
GO
ALTER TABLE [dbo].[MediaTag]  WITH CHECK ADD  CONSTRAINT [FK_MediaTag_Media] FOREIGN KEY([MediaId])
REFERENCES [dbo].[Media] ([Id])
GO
ALTER TABLE [dbo].[MediaTag] CHECK CONSTRAINT [FK_MediaTag_Media]
GO
ALTER TABLE [dbo].[MediaTag]  WITH CHECK ADD  CONSTRAINT [FK_MediaTag_Tag] FOREIGN KEY([TagId])
REFERENCES [dbo].[Tag] ([Id])
GO
ALTER TABLE [dbo].[MediaTag] CHECK CONSTRAINT [FK_MediaTag_Tag]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_Brand] FOREIGN KEY([BrandId])
REFERENCES [dbo].[Brand] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_Brand]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_CreationUser]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_SetupType] FOREIGN KEY([SetupTypeId])
REFERENCES [dbo].[SetupType] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_SetupType]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_ShippingClass] FOREIGN KEY([ShippingClassId])
REFERENCES [dbo].[ShippingClass] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_ShippingClass]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_Status] FOREIGN KEY([StatusId])
REFERENCES [dbo].[ProductStatus] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_Status]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_StockManagementType] FOREIGN KEY([StockManagementTypeId])
REFERENCES [dbo].[StockManagementType] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_StockManagementType]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_StockStatus] FOREIGN KEY([StockStatusId])
REFERENCES [dbo].[StockStatus] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_StockStatus]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_Supplier] FOREIGN KEY([SupplierId])
REFERENCES [dbo].[Supplier] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_Supplier]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_UpdateUser]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_Visibility] FOREIGN KEY([VisibilityId])
REFERENCES [dbo].[Visibility] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_Visibility]
GO
ALTER TABLE [dbo].[Product]  WITH CHECK ADD  CONSTRAINT [FK_Product_WeightConfig] FOREIGN KEY([WeightConfigId])
REFERENCES [dbo].[WeightConfig] ([Id])
GO
ALTER TABLE [dbo].[Product] CHECK CONSTRAINT [FK_Product_WeightConfig]
GO
ALTER TABLE [dbo].[ProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_ProductCategory_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[Category] ([Id])
GO
ALTER TABLE [dbo].[ProductCategory] CHECK CONSTRAINT [FK_ProductCategory_Category]
GO
ALTER TABLE [dbo].[ProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_ProductCategory_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductCategory] CHECK CONSTRAINT [FK_ProductCategory_Product]
GO
ALTER TABLE [dbo].[ProductImage]  WITH CHECK ADD  CONSTRAINT [FK_ProductImage_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductImage] CHECK CONSTRAINT [FK_ProductImage_Product]
GO
ALTER TABLE [dbo].[ProductOption]  WITH CHECK ADD  CONSTRAINT [FK_ProductOption_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductOption] CHECK CONSTRAINT [FK_ProductOption_Product]
GO
ALTER TABLE [dbo].[ProductOptionValue]  WITH CHECK ADD  CONSTRAINT [FK_ProductOptionValue_ProductOption] FOREIGN KEY([ProductOptionId])
REFERENCES [dbo].[ProductOption] ([Id])
GO
ALTER TABLE [dbo].[ProductOptionValue] CHECK CONSTRAINT [FK_ProductOptionValue_ProductOption]
GO
ALTER TABLE [dbo].[ProductSite]  WITH CHECK ADD  CONSTRAINT [FK_ProductSite_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductSite] CHECK CONSTRAINT [FK_ProductSite_Product]
GO
ALTER TABLE [dbo].[ProductSite]  WITH CHECK ADD  CONSTRAINT [FK_ProductSite_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[ProductSite] CHECK CONSTRAINT [FK_ProductSite_Site]
GO
ALTER TABLE [dbo].[ProductTag]  WITH CHECK ADD  CONSTRAINT [FK_ProductTag_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductTag] CHECK CONSTRAINT [FK_ProductTag_Product]
GO
ALTER TABLE [dbo].[ProductTag]  WITH CHECK ADD  CONSTRAINT [FK_ProductTag_Tag] FOREIGN KEY([TagId])
REFERENCES [dbo].[Tag] ([Id])
GO
ALTER TABLE [dbo].[ProductTag] CHECK CONSTRAINT [FK_ProductTag_Tag]
GO
ALTER TABLE [dbo].[ProductVariant]  WITH CHECK ADD  CONSTRAINT [FK_ProductVariant_Product] FOREIGN KEY([ProductId])
REFERENCES [dbo].[Product] ([Id])
GO
ALTER TABLE [dbo].[ProductVariant] CHECK CONSTRAINT [FK_ProductVariant_Product]
GO
ALTER TABLE [dbo].[ProductVariantOptionValue]  WITH CHECK ADD  CONSTRAINT [FK_ProductVariantOptionValue_ProductVariant] FOREIGN KEY([ProductVariantId])
REFERENCES [dbo].[ProductVariant] ([Id])
GO
ALTER TABLE [dbo].[ProductVariantOptionValue] CHECK CONSTRAINT [FK_ProductVariantOptionValue_ProductVariant]
GO
ALTER TABLE [dbo].[Site]  WITH CHECK ADD  CONSTRAINT [FK_Site_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Site] CHECK CONSTRAINT [FK_Site_Account]
GO
ALTER TABLE [dbo].[Site]  WITH CHECK ADD  CONSTRAINT [FK_Site_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Site] CHECK CONSTRAINT [FK_Site_CreationUser]
GO
ALTER TABLE [dbo].[Site]  WITH CHECK ADD  CONSTRAINT [FK_Site_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Site] CHECK CONSTRAINT [FK_Site_UpdateUser]
GO
ALTER TABLE [dbo].[SiteBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_SiteBusinessType_BusinessType] FOREIGN KEY([BusinessTypeId])
REFERENCES [dbo].[BusinessType] ([Id])
GO
ALTER TABLE [dbo].[SiteBusinessType] CHECK CONSTRAINT [FK_SiteBusinessType_BusinessType]
GO
ALTER TABLE [dbo].[SiteBusinessType]  WITH CHECK ADD  CONSTRAINT [FK_SiteBusinessType_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[SiteBusinessType] CHECK CONSTRAINT [FK_SiteBusinessType_Site]
GO
ALTER TABLE [dbo].[SiteUser]  WITH CHECK ADD  CONSTRAINT [FK_SiteUser_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[SiteUser] CHECK CONSTRAINT [FK_SiteUser_Site]
GO
ALTER TABLE [dbo].[SiteUser]  WITH CHECK ADD  CONSTRAINT [FK_SiteUser_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[SiteUser] CHECK CONSTRAINT [FK_SiteUser_User]
GO
ALTER TABLE [dbo].[Supplier]  WITH CHECK ADD  CONSTRAINT [FK_Supplier_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Supplier] CHECK CONSTRAINT [FK_Supplier_Account]
GO
ALTER TABLE [dbo].[Supplier]  WITH CHECK ADD  CONSTRAINT [FK_Supplier_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Supplier] CHECK CONSTRAINT [FK_Supplier_CreationUser]
GO
ALTER TABLE [dbo].[Supplier]  WITH CHECK ADD  CONSTRAINT [FK_Supplier_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Supplier] CHECK CONSTRAINT [FK_Supplier_UpdateUser]
GO
ALTER TABLE [dbo].[Tag]  WITH CHECK ADD  CONSTRAINT [FK_Tag_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[Tag] CHECK CONSTRAINT [FK_Tag_Account]
GO
ALTER TABLE [dbo].[Tag]  WITH CHECK ADD  CONSTRAINT [FK_Tag_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Tag] CHECK CONSTRAINT [FK_Tag_CreationUser]
GO
ALTER TABLE [dbo].[Tag]  WITH CHECK ADD  CONSTRAINT [FK_Tag_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[Tag] CHECK CONSTRAINT [FK_Tag_UpdateUser]
GO
ALTER TABLE [dbo].[TemplateAttribute]  WITH CHECK ADD  CONSTRAINT [FK_TemplateAttribute_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[TemplateAttribute] CHECK CONSTRAINT [FK_TemplateAttribute_CreationUser]
GO
ALTER TABLE [dbo].[TemplateAttribute]  WITH CHECK ADD  CONSTRAINT [FK_TemplateAttribute_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[TemplateAttribute] CHECK CONSTRAINT [FK_TemplateAttribute_UpdateUser]
GO
ALTER TABLE [dbo].[TemplateAttributeSite]  WITH CHECK ADD  CONSTRAINT [FK_TAS_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[TemplateAttributeSite] CHECK CONSTRAINT [FK_TAS_Site]
GO
ALTER TABLE [dbo].[TemplateAttributeSite]  WITH CHECK ADD  CONSTRAINT [FK_TAS_TemplateAttribute] FOREIGN KEY([TemplateAttributeId])
REFERENCES [dbo].[TemplateAttribute] ([Id])
GO
ALTER TABLE [dbo].[TemplateAttributeSite] CHECK CONSTRAINT [FK_TAS_TemplateAttribute]
GO
ALTER TABLE [dbo].[TemplateAttributeValue]  WITH CHECK ADD  CONSTRAINT [FK_TemplateAttributeValue_TemplateAttribute] FOREIGN KEY([TemplateAttributeId])
REFERENCES [dbo].[TemplateAttribute] ([Id])
GO
ALTER TABLE [dbo].[TemplateAttributeValue] CHECK CONSTRAINT [FK_TemplateAttributeValue_TemplateAttribute]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_Brand] FOREIGN KEY([BrandId])
REFERENCES [dbo].[Brand] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_Brand]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_CreationUser]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_SetupType] FOREIGN KEY([SetupTypeId])
REFERENCES [dbo].[SetupType] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_SetupType]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_ShippingClass] FOREIGN KEY([ShippingClassId])
REFERENCES [dbo].[ShippingClass] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_ShippingClass]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_Status] FOREIGN KEY([StatusId])
REFERENCES [dbo].[ProductStatus] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_Status]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_StockManagementType] FOREIGN KEY([StockManagementTypeId])
REFERENCES [dbo].[StockManagementType] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_StockManagementType]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_StockStatus] FOREIGN KEY([StockStatusId])
REFERENCES [dbo].[StockStatus] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_StockStatus]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_Supplier] FOREIGN KEY([SupplierId])
REFERENCES [dbo].[Supplier] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_Supplier]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_UpdateUser]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_Visibility] FOREIGN KEY([VisibilityId])
REFERENCES [dbo].[Visibility] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_Visibility]
GO
ALTER TABLE [dbo].[TemplateProduct]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProduct_WeightConfig] FOREIGN KEY([WeightConfigId])
REFERENCES [dbo].[WeightConfig] ([Id])
GO
ALTER TABLE [dbo].[TemplateProduct] CHECK CONSTRAINT [FK_TemplateProduct_WeightConfig]
GO
ALTER TABLE [dbo].[TemplateProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_TPC_GlobalCategory] FOREIGN KEY([GlobalCategoryId])
REFERENCES [dbo].[GlobalCategory] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductCategory] CHECK CONSTRAINT [FK_TPC_GlobalCategory]
GO
ALTER TABLE [dbo].[TemplateProductCategory]  WITH CHECK ADD  CONSTRAINT [FK_TPC_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductCategory] CHECK CONSTRAINT [FK_TPC_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductImage]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProductImage_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductImage] CHECK CONSTRAINT [FK_TemplateProductImage_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductOption]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProductOption_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductOption] CHECK CONSTRAINT [FK_TemplateProductOption_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductOptionValue]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProductOptionValue_TemplateProductOption] FOREIGN KEY([TemplateProductOptionId])
REFERENCES [dbo].[TemplateProductOption] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductOptionValue] CHECK CONSTRAINT [FK_TemplateProductOptionValue_TemplateProductOption]
GO
ALTER TABLE [dbo].[TemplateProductSite]  WITH CHECK ADD  CONSTRAINT [FK_TPS_Site] FOREIGN KEY([SiteId])
REFERENCES [dbo].[Site] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductSite] CHECK CONSTRAINT [FK_TPS_Site]
GO
ALTER TABLE [dbo].[TemplateProductSite]  WITH CHECK ADD  CONSTRAINT [FK_TPS_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductSite] CHECK CONSTRAINT [FK_TPS_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductTag]  WITH CHECK ADD  CONSTRAINT [FK_TPT_Tag] FOREIGN KEY([TagId])
REFERENCES [dbo].[Tag] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductTag] CHECK CONSTRAINT [FK_TPT_Tag]
GO
ALTER TABLE [dbo].[TemplateProductTag]  WITH CHECK ADD  CONSTRAINT [FK_TPT_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductTag] CHECK CONSTRAINT [FK_TPT_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductVariant]  WITH CHECK ADD  CONSTRAINT [FK_TemplateProductVariant_TemplateProduct] FOREIGN KEY([TemplateProductId])
REFERENCES [dbo].[TemplateProduct] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductVariant] CHECK CONSTRAINT [FK_TemplateProductVariant_TemplateProduct]
GO
ALTER TABLE [dbo].[TemplateProductVariantOptionValue]  WITH CHECK ADD  CONSTRAINT [FK_TPVOV_TemplateProductVariant] FOREIGN KEY([TemplateProductVariantId])
REFERENCES [dbo].[TemplateProductVariant] ([Id])
GO
ALTER TABLE [dbo].[TemplateProductVariantOptionValue] CHECK CONSTRAINT [FK_TPVOV_TemplateProductVariant]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Account] ([Id])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_Account]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_CreationUser] FOREIGN KEY([CreationUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_CreationUser]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_Role] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Role] ([Id])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_Role]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_Status] FOREIGN KEY([StatusId])
REFERENCES [dbo].[UserStatus] ([Id])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_Status]
GO
ALTER TABLE [dbo].[User]  WITH CHECK ADD  CONSTRAINT [FK_User_UpdateUser] FOREIGN KEY([UpdateUserId])
REFERENCES [dbo].[User] ([Id])
GO
ALTER TABLE [dbo].[User] CHECK CONSTRAINT [FK_User_UpdateUser]
GO
ALTER TABLE [dbo].[WeightConfig]  WITH CHECK ADD  CONSTRAINT [FK_WeightConfig_Unit] FOREIGN KEY([UnitId])
REFERENCES [dbo].[Unit] ([Id])
GO
ALTER TABLE [dbo].[WeightConfig] CHECK CONSTRAINT [FK_WeightConfig_Unit]
GO
ALTER TABLE [dbo].[WeightConfig]  WITH CHECK ADD  CONSTRAINT [FK_WeightConfig_UnitWeightMode] FOREIGN KEY([UnitWeightModeId])
REFERENCES [dbo].[UnitWeightMode] ([Id])
GO
ALTER TABLE [dbo].[WeightConfig] CHECK CONSTRAINT [FK_WeightConfig_UnitWeightMode]
GO
