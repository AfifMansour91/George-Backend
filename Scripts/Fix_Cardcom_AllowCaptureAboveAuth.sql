-- 2026-09-27: the frontend "הסכום גבוה מהמסגרת שנתפסה" picking block (deployed 26/9 for PEPE / PayPlus)
-- fired on every Cardcom site whose picked total exceeded the hold (Zano Dagim, site 45, buffer 0%).
-- Cardcom picking never captures the hold: it voids the J5 and charges the token for the full amount,
-- so the hold does not limit the charge. Site.PaymentAllowCaptureAboveAuth is not read by the backend at all;
-- it only gates the frontend dialog. Setting it ON for every Cardcom site disables the block until the
-- frontend fix (guard limited to PayPlus) is deployed. Safe to leave ON afterwards.
-- Idempotent. Re-run any time.

SET NOCOUNT ON;

SELECT Id, AccountId, PaymentAllowCaptureAboveAuth AS Before_AllowAbove
FROM dbo.Site
WHERE LOWER(LTRIM(RTRIM(PaymentGatewayProvider))) = 'cardcom'
  AND PaymentAllowCaptureAboveAuth = 0
ORDER BY Id;

UPDATE dbo.Site
SET PaymentAllowCaptureAboveAuth = 1
WHERE LOWER(LTRIM(RTRIM(PaymentGatewayProvider))) = 'cardcom'
  AND PaymentAllowCaptureAboveAuth = 0;

PRINT CONCAT('Cardcom sites updated: ', @@ROWCOUNT);

SELECT Id, AccountId, PaymentAllowCaptureAboveAuth AS After_AllowAbove
FROM dbo.Site
WHERE LOWER(LTRIM(RTRIM(PaymentGatewayProvider))) = 'cardcom'
ORDER BY Id;
