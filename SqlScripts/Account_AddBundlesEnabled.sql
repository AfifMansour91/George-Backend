-- Bundles (מארזים): account-level feature gate (pattern: Account.KioskEnabled).
-- Spec: BUNDLES_SYNC_SPEC.md §1 / §3.4. Idempotent.
-- Default OFF: adding the column leaves the module OFF for EVERY existing account (and for rows inserted outside the
-- API). It is switched on per account by a super admin (חשבונות → עריכה → "מארזים"); to switch it on for everyone
-- at once there is the separate, optional Account_EnableBundlesForAll.sql.

IF COL_LENGTH(N'dbo.Account', N'BundlesEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Account ADD
        BundlesEnabled BIT NOT NULL CONSTRAINT DF_Account_BundlesEnabled DEFAULT (0);
END
GO
