-- Add new kiosk settings columns (Sprint2: POS enable, button texts, inactivity popup).
-- Run this once when upgrading from a version that did not have these columns.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'PosProductsEnabled')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD PosProductsEnabled BIT NOT NULL DEFAULT 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'ButtonTextToPaymentOrViewOrder')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD ButtonTextToPaymentOrViewOrder NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'ButtonTextCartToPayment')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD ButtonTextCartToPayment NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'ButtonTextUpsellContinueToPayment')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD ButtonTextUpsellContinueToPayment NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'InactivityPopupSeconds')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD InactivityPopupSeconds INT NULL;
END
GO

-- Privacy policy on phone screen: checkbox default + content (panel)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'PrivacyPolicyCheckboxCheckedByDefault')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD PrivacyPolicyCheckboxCheckedByDefault BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.KioskSettings') AND name = 'PrivacyPolicyContent')
BEGIN
    ALTER TABLE dbo.KioskSettings ADD PrivacyPolicyContent NVARCHAR(MAX) NULL;
END
GO
