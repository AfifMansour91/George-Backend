-- Order handler (מטפל): the staff member handling the order — the user who created a manual/phone
-- order, or the first user to take a website order into treatment (start-treatment / picking).
-- HandlerUserId references the staff User; HandlerName is denormalized from User.FullName at stamp
-- time so cards/printouts can show it without a join. Shown only when Site.ShowOrderHandler is on.
-- Idempotent: each step is guarded so the script can be re-run safely.

IF COL_LENGTH(N'dbo.[Order]', N'HandlerUserId') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD HandlerUserId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'HandlerName') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD HandlerName NVARCHAR(200) NULL;
END
GO

-- Backfill existing orders: prefer the creating user (manual/phone orders), else the picker.
UPDATE o
SET o.HandlerUserId = o.CreationUserId,
    o.HandlerName = u.FullName
FROM dbo.[Order] o
JOIN dbo.[User] u ON u.Id = o.CreationUserId
WHERE o.HandlerUserId IS NULL
  AND o.CreationUserId IS NOT NULL;
GO

UPDATE dbo.[Order]
SET HandlerUserId = PickerUserId,
    HandlerName = PickerName
WHERE HandlerUserId IS NULL
  AND PickerUserId IS NOT NULL;
GO
