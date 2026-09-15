-- Bundles (מארזים): account-level feature gate (pattern: Account.KioskEnabled).
-- Spec: BUNDLES_SYNC_SPEC.md §1 / §3.4. Idempotent.
-- Default ON: the module is enabled for every new account (existing accounts: Account_EnableBundlesForAll.sql).

IF COL_LENGTH(N'dbo.Account', N'BundlesEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Account ADD
        BundlesEnabled BIT NOT NULL CONSTRAINT DF_Account_BundlesEnabled DEFAULT (1);
END
GO
