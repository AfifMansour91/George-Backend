-- Marketing: MarketingSend.BypassSendWindow (George's feedback 15.10.2026).
-- "שלח עכשיו" is the shop's own decision and goes out immediately, even outside the send hours / on Shabbat;
-- a scheduled send may also be flagged by the shop ("שלח בכל זאת במועד שבחרתי"). Automations will never set it.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.MarketingSend', N'BypassSendWindow') IS NULL
BEGIN
    ALTER TABLE [dbo].[MarketingSend] ADD [BypassSendWindow] BIT NOT NULL CONSTRAINT [DF_MarketingSend_BypassSendWindow] DEFAULT (0);
    PRINT 'Added MarketingSend.BypassSendWindow';
END
ELSE PRINT 'MarketingSend.BypassSendWindow already exists';
GO
