-- Add Role for kiosk customers (duplicate order / past products flow).
-- Run once. Id must match UserRole.KioskCustomer = 4 in code.
IF NOT EXISTS (SELECT 1 FROM [dbo].[Role] WHERE [Name] = N'kiosk_customer')
BEGIN
    SET IDENTITY_INSERT [dbo].[Role] ON;
    INSERT [dbo].[Role] ([Id], [Name], [IsDeleted]) VALUES (4, N'kiosk_customer', 0);
    SET IDENTITY_INSERT [dbo].[Role] OFF;
END
GO
