-- Product storefront labels (תוויות) + account default for "חדש" duration (days)
IF COL_LENGTH('dbo.Product', 'LabelFrozen') IS NULL
    ALTER TABLE dbo.Product ADD LabelFrozen bit NOT NULL CONSTRAINT DF_Product_LabelFrozen DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelGlutenFree') IS NULL
    ALTER TABLE dbo.Product ADD LabelGlutenFree bit NOT NULL CONSTRAINT DF_Product_LabelGlutenFree DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelNotKosher') IS NULL
    ALTER TABLE dbo.Product ADD LabelNotKosher bit NOT NULL CONSTRAINT DF_Product_LabelNotKosher DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelKosherForPassover') IS NULL
    ALTER TABLE dbo.Product ADD LabelKosherForPassover bit NOT NULL CONSTRAINT DF_Product_LabelKosherForPassover DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelKosherForPassoverEndDate') IS NULL
    ALTER TABLE dbo.Product ADD LabelKosherForPassoverEndDate datetime2(0) NULL;

IF COL_LENGTH('dbo.Product', 'LabelNew') IS NULL
    ALTER TABLE dbo.Product ADD LabelNew bit NOT NULL CONSTRAINT DF_Product_LabelNew DEFAULT (0);

IF COL_LENGTH('dbo.Product', 'LabelNewEndDate') IS NULL
    ALTER TABLE dbo.Product ADD LabelNewEndDate datetime2(0) NULL;

IF COL_LENGTH('dbo.Account', 'DefaultNewLabelDays') IS NULL
    ALTER TABLE dbo.Account ADD DefaultNewLabelDays int NOT NULL CONSTRAINT DF_Account_DefaultNewLabelDays DEFAULT (7);
