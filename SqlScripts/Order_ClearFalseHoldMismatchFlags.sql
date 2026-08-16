-- One-time cleanup for false "hold but marked captured" mismatch flags (Zano-Dagim reports).
--
-- Background: capturing a suspended/J5 Cardcom deal creates a NEW transaction id, but the Woo
-- plugin keeps reporting the original hold's id. Verification then saw "hold" on a captured order
-- and wrote GatewayAmountMismatch = 1 with GatewayVerifiedAmount = 0 — even though the charge ran
-- (its Cardcom document is on the order). The code now recognizes this (HoldWithCaptureEvidence)
-- and clears the flag on the next verify; this script clears the already-flagged orders in bulk.
--
-- Signature targeted: mismatch flag raised with verified amount 0 (the hold-but-captured path —
-- a genuine amount mismatch stores the actual nonzero charged amount) AND a Cardcom charge
-- document present on the order. Genuine Delinka-style false successes have no document and are
-- left flagged. Idempotent: re-running affects no additional rows.

SELECT Id, SiteId, Total, GatewayVerifiedAmount, GatewayVerifiedAt, InvoiceNumber
FROM dbo.[Order]
WHERE GatewayAmountMismatch = 1
  AND GatewayVerifiedAmount = 0
  AND PaymentSettleStatus = N'Captured'
  AND (NULLIF(LTRIM(RTRIM(InvoiceNumber)), N'') IS NOT NULL
       OR NULLIF(LTRIM(RTRIM(CardcomDocumentUrl)), N'') IS NOT NULL);

UPDATE dbo.[Order]
SET GatewayAmountMismatch = 0,
    GatewayVerifiedAmount = NULL
WHERE GatewayAmountMismatch = 1
  AND GatewayVerifiedAmount = 0
  AND PaymentSettleStatus = N'Captured'
  AND (NULLIF(LTRIM(RTRIM(InvoiceNumber)), N'') IS NOT NULL
       OR NULLIF(LTRIM(RTRIM(CardcomDocumentUrl)), N'') IS NOT NULL);
