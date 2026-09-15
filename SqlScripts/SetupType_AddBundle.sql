-- Bundles (מארזים): a bundle is a Product with SetupType 'bundle' (Id = 5).
-- Spec: shop-manager/docs/wooCommerceEngines/BUNDLES_SYNC_SPEC.md §1.
-- Idempotent: inserts the lookup row only when missing (by name or by id).

IF NOT EXISTS (SELECT 1 FROM dbo.SetupType WHERE Name = N'bundle')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.SetupType WHERE Id = 5)
    BEGIN
        SET IDENTITY_INSERT dbo.SetupType ON;
        INSERT INTO dbo.SetupType (Id, Name, IsDeleted) VALUES (5, N'bundle', 0);
        SET IDENTITY_INSERT dbo.SetupType OFF;
    END
    ELSE
    BEGIN
        -- Id 5 is already taken by another row: let IDENTITY pick the next id (the code resolves by name).
        INSERT INTO dbo.SetupType (Name, IsDeleted) VALUES (N'bundle', 0);
    END
END
GO
