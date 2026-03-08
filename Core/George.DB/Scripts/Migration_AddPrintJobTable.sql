-- Print jobs for local print agent (multi-branch silent printing).
-- React sends print request -> API stores job -> local agent at branch polls and prints.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PrintJob')
BEGIN
    CREATE TABLE [dbo].[PrintJob] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SiteId] INT NOT NULL,
        [OrderId] INT NULL,
        [JobType] NVARCHAR(50) NOT NULL,
        [Payload] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT DF_PrintJob_CreatedAt DEFAULT (SYSUTCDATETIME()),
        [PrintedAt] DATETIME2(7) NULL,
        [AgentId] NVARCHAR(100) NULL,
        [ErrorMessage] NVARCHAR(500) NULL,
        CONSTRAINT [PK_PrintJob] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrintJob_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site]([Id])
    );
    CREATE INDEX [IX_PrintJob_SiteId_Status] ON [dbo].[PrintJob]([SiteId], [Status]);
END
GO

-- Site: "use local print agent" per branch. Default 1 (true) so new sites use the print agent.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'VoucherPrinterUseAgent')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherPrinterUseAgent] BIT NOT NULL CONSTRAINT DF_Site_VoucherPrinterUseAgent DEFAULT 1;
END
ELSE
BEGIN
    -- Column exists (e.g. from earlier migration with default 0): switch default to 1 and set existing rows to 1.
    ALTER TABLE [dbo].[Site] DROP CONSTRAINT IF EXISTS DF_Site_VoucherPrinterUseAgent;
    ALTER TABLE [dbo].[Site] ADD CONSTRAINT DF_Site_VoucherPrinterUseAgent DEFAULT 1 FOR [VoucherPrinterUseAgent];
    UPDATE [dbo].[Site] SET [VoucherPrinterUseAgent] = 1 WHERE [VoucherPrinterUseAgent] = 0;
END
GO
