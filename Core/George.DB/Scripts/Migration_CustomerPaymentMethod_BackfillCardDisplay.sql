-- Backfill Last4Digits / CardBrand / TokenExDate on CustomerPaymentMethod from the latest
-- successful ValidateCallback or ChargeToken JSON on linked orders (Cardcom).
-- Run once after deploying the PaymentService card-display fix. Review results before commit.

;WITH LatestEvent AS (
    SELECT
        o.CustomerPaymentMethodId AS PaymentMethodId,
        e.RawResponseJson,
        ROW_NUMBER() OVER (
            PARTITION BY o.CustomerPaymentMethodId
            ORDER BY e.CreationTime DESC
        ) AS rn
    FROM dbo.[Order] o
    INNER JOIN dbo.OrderPaymentEvent e ON e.OrderId = o.Id
    WHERE o.CustomerPaymentMethodId IS NOT NULL
      AND e.StatusCode = '0'
      AND e.EventType IN (N'ValidateCallback', N'ChargeToken')
      AND e.RawResponseJson IS NOT NULL
      AND LEN(e.RawResponseJson) > 50
),
Parsed AS (
    SELECT
        le.PaymentMethodId,
        JSON_VALUE(le.RawResponseJson, '$.TranzactionInfo.Last4CardDigitsString') AS Last4FromTi,
        JSON_VALUE(le.RawResponseJson, '$.Last4CardDigitsString') AS Last4FromRoot,
        JSON_VALUE(le.RawResponseJson, '$.TranzactionInfo.Brand') AS BrandFromTi,
        JSON_VALUE(le.RawResponseJson, '$.Brand') AS BrandFromRoot,
        JSON_VALUE(le.RawResponseJson, '$.TokenInfo.TokenExDate') AS TokenExFromInfo
    FROM LatestEvent le
    WHERE le.rn = 1
)
UPDATE pm
SET
    Last4Digits = COALESCE(
        NULLIF(pm.Last4Digits, N''),
        NULLIF(RIGHT(REPLACE(COALESCE(p.Last4FromTi, p.Last4FromRoot, N''), N' ', N''), 4), N''),
        pm.Last4Digits),
    CardBrand = COALESCE(NULLIF(pm.CardBrand, N''), NULLIF(p.BrandFromTi, N''), NULLIF(p.BrandFromRoot, N''), pm.CardBrand),
    TokenExDate = COALESCE(NULLIF(pm.TokenExDate, N''), NULLIF(p.TokenExFromInfo, N''), pm.TokenExDate)
FROM dbo.CustomerPaymentMethod pm
INNER JOIN Parsed p ON p.PaymentMethodId = pm.Id
WHERE pm.IsRetired = 0
  AND (
        (pm.Last4Digits IS NULL OR LTRIM(RTRIM(pm.Last4Digits)) = N'')
     OR (pm.CardBrand IS NULL OR LTRIM(RTRIM(pm.CardBrand)) = N'')
     OR (pm.TokenExDate IS NULL OR LTRIM(RTRIM(pm.TokenExDate)) = N'')
  );
