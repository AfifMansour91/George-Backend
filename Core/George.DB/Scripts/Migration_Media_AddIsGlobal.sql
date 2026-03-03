-- Migration: Add IsGlobal to Media so global media is explicitly marked.
-- Date: 2026-03-02
-- Description: When true, media belongs to the global library (super-admin). List global media by IsGlobal = 1.
--              Backfill: set IsGlobal = 1 for media that are not in AccountMedia (current "global" definition).
--              Safe to run multiple times (idempotent where possible).

GO

-- 1. Add IsGlobal column (default 0 = account media)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Media]') AND name = N'IsGlobal')
BEGIN
    ALTER TABLE [dbo].[Media] ADD [IsGlobal] [bit] NOT NULL CONSTRAINT [DF_Media_IsGlobal] DEFAULT 0;
    PRINT 'Added Media.IsGlobal (default 0)';
END
GO

-- 2. Backfill: set IsGlobal = 1 for media that are not in AccountMedia
UPDATE m
SET m.[IsGlobal] = 1
FROM [dbo].[Media] m
WHERE m.[IsDeleted] = 0
  AND NOT EXISTS (SELECT 1 FROM [dbo].[AccountMedia] am WHERE am.[MediaId] = m.[Id]);
IF @@ROWCOUNT > 0
    PRINT 'Backfilled Media.IsGlobal = 1 for media not in AccountMedia';
GO

PRINT 'Migration_Media_AddIsGlobal completed successfully'
GO
