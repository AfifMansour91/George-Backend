-- =============================================================================
-- PREVIEW (read-only) for a future Backfill_Customer_CanonicalizePhone.sql.
-- Shows which Customer rows would be re-normalized, and which would MERGE
-- (two+ rows in the same site collapsing onto the same canonical phone) once
-- Customer.NormalizedPhone switches from raw digit-strip to the canonical form
-- used by CustomerStorage.CanonicalizeImportPhone (George.Data):
--   972-prefixed 11/12-digit number -> replace "972" with a leading "0"
--   8/9-digit number not already starting with "0" -> prefix "0"
--   else unchanged
-- Runs NO updates - safe on Prod anytime.
-- =============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#Canon') IS NOT NULL DROP TABLE #Canon;

SELECT
    c.Id, c.SiteId, c.NormalizedPhone, c.Phone, c.Name, c.CreationTime, c.UpdatedDate,
    CASE
        WHEN LEN(c.NormalizedPhone) IN (11,12) AND LEFT(c.NormalizedPhone,3) = '972'
            THEN '0' + SUBSTRING(c.NormalizedPhone,4,20)
        WHEN LEN(c.NormalizedPhone) IN (8,9) AND LEFT(c.NormalizedPhone,1) <> '0'
            THEN '0' + c.NormalizedPhone
        ELSE c.NormalizedPhone
    END AS CanonicalPhone
INTO #Canon
FROM dbo.Customer c
WHERE c.IsDeleted = 0 AND c.NormalizedPhone <> '';

-- -----------------------------------------------------------------------------
-- 1. Rows whose NormalizedPhone would simply be REWRITTEN in place - the only
--    row at that site with this canonical phone, so no merge is needed.
-- -----------------------------------------------------------------------------
SELECT
    x.SiteId, x.Id AS CustomerId, x.Name,
    x.NormalizedPhone AS Phone_Before, x.CanonicalPhone AS Phone_After
FROM #Canon x
WHERE x.CanonicalPhone <> x.NormalizedPhone
  AND NOT EXISTS (
      SELECT 1 FROM #Canon y
      WHERE y.SiteId = x.SiteId AND y.CanonicalPhone = x.CanonicalPhone AND y.Id <> x.Id
  )
ORDER BY x.SiteId, x.Id;

-- -----------------------------------------------------------------------------
-- 2. Duplicate groups that would MERGE: two+ existing rows in the same site
--    collapsing onto the same canonical phone. Per row: live order count and
--    saved-payment-method count, to help pick which row is the "winner" the
--    others get merged into (Order.CustomerId / CustomerPaymentMethod.CustomerId
--    reassigned to the winner, losers soft-deleted).
-- -----------------------------------------------------------------------------
;WITH Dup AS (
    SELECT SiteId, CanonicalPhone
    FROM #Canon
    GROUP BY SiteId, CanonicalPhone
    HAVING COUNT(*) > 1
)
SELECT
    x.SiteId, x.CanonicalPhone, x.Id AS CustomerId, x.Name,
    x.NormalizedPhone AS Phone_Before, x.Phone AS RawPhone, x.CreationTime, x.UpdatedDate,
    (SELECT COUNT(*) FROM dbo.[Order] o WHERE o.CustomerId = x.Id AND o.IsDeleted = 0) AS OrderCount,
    (SELECT COUNT(*) FROM dbo.CustomerPaymentMethod pm WHERE pm.CustomerId = x.Id AND pm.IsRetired = 0) AS SavedPaymentMethodCount
FROM #Canon x
JOIN Dup d ON d.SiteId = x.SiteId AND d.CanonicalPhone = x.CanonicalPhone
ORDER BY x.SiteId, x.CanonicalPhone,
    (SELECT COUNT(*) FROM dbo.[Order] o WHERE o.CustomerId = x.Id AND o.IsDeleted = 0) DESC;

-- -----------------------------------------------------------------------------
-- 3. Summary counts.
-- -----------------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM #Canon)                                          AS CustomersInScope,
    (SELECT COUNT(*) FROM #Canon WHERE CanonicalPhone <> NormalizedPhone)  AS RowsNeedingRewrite,
    (SELECT COUNT(*) FROM (
        SELECT SiteId, CanonicalPhone FROM #Canon
        GROUP BY SiteId, CanonicalPhone HAVING COUNT(*) > 1
    ) g)                                                                    AS DuplicateGroups,
    (SELECT COUNT(*) FROM #Canon x WHERE EXISTS (
        SELECT 1 FROM #Canon y
        WHERE y.SiteId = x.SiteId AND y.CanonicalPhone = x.CanonicalPhone AND y.Id <> x.Id
    ))                                                                      AS RowsInvolvedInMerges;
