-- New-order ring that keeps repeating until a user confirms it (Meshek Basar 2026-10-23).
--   NewOrder_ManagerSoundRepeatUntilAck        - 0 (default) = ring once per order (today); 1 = ring again every
--                                                N seconds until someone clicks "אישור" on the alert or the order
--                                                leaves the "New" column.
--   NewOrder_ManagerSoundRepeatIntervalSeconds - seconds between repeats (default 30).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]')
      AND name = 'NewOrder_ManagerSoundRepeatUntilAck'
)
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD [NewOrder_ManagerSoundRepeatUntilAck] [bit] NOT NULL
        CONSTRAINT [DF_AccountNotificationSettings_NewOrder_ManagerSoundRepeatUntilAck] DEFAULT (0);
    PRINT 'Added NewOrder_ManagerSoundRepeatUntilAck';
END
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]')
      AND name = 'NewOrder_ManagerSoundRepeatIntervalSeconds'
)
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD [NewOrder_ManagerSoundRepeatIntervalSeconds] [int] NOT NULL
        CONSTRAINT [DF_AccountNotificationSettings_NewOrder_ManagerSoundRepeatIntervalSeconds] DEFAULT (30);
    PRINT 'Added NewOrder_ManagerSoundRepeatIntervalSeconds';
END
GO
