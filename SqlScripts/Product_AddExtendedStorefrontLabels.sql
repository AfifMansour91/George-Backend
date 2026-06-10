-- Run on George DB before deploying API that maps Product.LabelBestseller / LabelReadyToCook / LabelNatural / LabelSugarFree / LabelLactoseFree (תוויות חנות נוספות).
-- Woo ED/v1 routes: product-bestseller, product-readytocook, product-natural, product-sugarfree, product-lactosefree.

IF COL_LENGTH('dbo.Product', 'LabelBestseller') IS NULL
    ALTER TABLE dbo.Product ADD LabelBestseller BIT NOT NULL CONSTRAINT DF_Product_LabelBestseller DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelReadyToCook') IS NULL
    ALTER TABLE dbo.Product ADD LabelReadyToCook BIT NOT NULL CONSTRAINT DF_Product_LabelReadyToCook DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelNatural') IS NULL
    ALTER TABLE dbo.Product ADD LabelNatural BIT NOT NULL CONSTRAINT DF_Product_LabelNatural DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelSugarFree') IS NULL
    ALTER TABLE dbo.Product ADD LabelSugarFree BIT NOT NULL CONSTRAINT DF_Product_LabelSugarFree DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelLactoseFree') IS NULL
    ALTER TABLE dbo.Product ADD LabelLactoseFree BIT NOT NULL CONSTRAINT DF_Product_LabelLactoseFree DEFAULT (0);
