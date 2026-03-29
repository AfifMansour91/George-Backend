-- Run once on existing databases: add "no wizard" type for accounts that already have a store.
IF NOT EXISTS (SELECT 1 FROM [dbo].[WizardType] WHERE [Name] = N'none' AND [IsDeleted] = 0)
BEGIN
    SET IDENTITY_INSERT [dbo].[WizardType] ON;
    INSERT [dbo].[WizardType] ([Id], [Name], [IsDeleted]) VALUES (3, N'none', 0);
    SET IDENTITY_INSERT [dbo].[WizardType] OFF;
END
