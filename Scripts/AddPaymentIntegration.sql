-- Cardcom payment integration (Site settings, order payment state, saved cards, audit log)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Site') AND name = 'PaymentGatewayProvider')
BEGIN
    ALTER TABLE dbo.Site ADD
        PaymentGatewayProvider NVARCHAR(32) NOT NULL CONSTRAINT DF_Site_PaymentGatewayProvider DEFAULT ('none'),
        CardcomTerminalNumber INT NULL,
        CardcomApiName NVARCHAR(100) NULL,
        CardcomApiPasswordEncrypted NVARCHAR(500) NULL,
        CardcomSaveCardEnabled BIT NOT NULL CONSTRAINT DF_Site_CardcomSaveCardEnabled DEFAULT (1),
        PaymentAuthBufferPercent INT NOT NULL CONSTRAINT DF_Site_PaymentAuthBufferPercent DEFAULT (25),
        PaymentMaxAuthAmount DECIMAL(18,2) NULL,
        PaymentAllowCaptureAboveAuth BIT NOT NULL CONSTRAINT DF_Site_PaymentAllowCaptureAboveAuth DEFAULT (0),
        CardcomProviderExtrasJson NVARCHAR(2000) NULL,
        CardcomCssUrl NVARCHAR(500) NULL,
        CardcomLogoUrl NVARCHAR(500) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'PaymentSettleStatus')
BEGIN
    ALTER TABLE dbo.[Order] ADD
        PaymentSettleStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_Order_PaymentSettleStatus DEFAULT ('None'),
        PaymentAuthorizedAmount DECIMAL(18,2) NULL,
        CardcomLowProfileId NVARCHAR(64) NULL,
        CardcomSuspendedDealId NVARCHAR(32) NULL,
        CardcomApprovalNumber NVARCHAR(32) NULL,
        CardcomTokenLast4 NVARCHAR(8) NULL,
        CardcomCardBrand NVARCHAR(32) NULL,
        CustomerPaymentMethodId INT NULL;
END
GO

IF OBJECT_ID(N'dbo.CustomerPaymentMethod', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerPaymentMethod (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NOT NULL,
        SiteId INT NOT NULL,
        EncryptedToken NVARCHAR(1000) NOT NULL,
        TokenExDate NVARCHAR(16) NULL,
        CardExpirationMMYY NVARCHAR(8) NULL,
        Last4Digits NVARCHAR(8) NULL,
        CardBrand NVARCHAR(32) NULL,
        EncryptedApprovalNumber NVARCHAR(500) NULL,
        IsDefault BIT NOT NULL CONSTRAINT DF_CustomerPaymentMethod_IsDefault DEFAULT (0),
        IsRetired BIT NOT NULL CONSTRAINT DF_CustomerPaymentMethod_IsRetired DEFAULT (0),
        CreationTime DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerPaymentMethod_CreationTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CustomerPaymentMethod_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(Id),
        CONSTRAINT FK_CustomerPaymentMethod_Site FOREIGN KEY (SiteId) REFERENCES dbo.Site(Id)
    );
    CREATE INDEX IX_CustomerPaymentMethod_Customer_Site ON dbo.CustomerPaymentMethod(CustomerId, SiteId) WHERE IsRetired = 0;
END
GO

IF OBJECT_ID(N'dbo.OrderPaymentEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderPaymentEvent (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId INT NOT NULL,
        EventType NVARCHAR(64) NOT NULL,
        Provider NVARCHAR(32) NOT NULL,
        StatusCode NVARCHAR(32) NULL,
        Description NVARCHAR(500) NULL,
        GatewayTransactionId NVARCHAR(64) NULL,
        MaskedToken NVARCHAR(32) NULL,
        Amount DECIMAL(18,2) NULL,
        RawResponseJson NVARCHAR(MAX) NULL,
        CreationTime DATETIME2(0) NOT NULL CONSTRAINT DF_OrderPaymentEvent_CreationTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_OrderPaymentEvent_Order FOREIGN KEY (OrderId) REFERENCES dbo.[Order](Id)
    );
    CREATE INDEX IX_OrderPaymentEvent_OrderId ON dbo.OrderPaymentEvent(OrderId, CreationTime DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Order_CustomerPaymentMethod')
BEGIN
    ALTER TABLE dbo.[Order] ADD CONSTRAINT FK_Order_CustomerPaymentMethod
        FOREIGN KEY (CustomerPaymentMethodId) REFERENCES dbo.CustomerPaymentMethod(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'CardcomDocumentUrl')
BEGIN
    ALTER TABLE dbo.[Order] ADD CardcomDocumentUrl NVARCHAR(1000) NULL;
END
GO
