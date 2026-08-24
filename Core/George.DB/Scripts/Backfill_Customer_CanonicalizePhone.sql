/*
  Backfill Customer.NormalizedPhone to the canonical form used by
  CustomerStorage.CanonicalizeImportPhone (George.Data): 972-prefixed 11/12-digit
  numbers -> "0" + rest; 8/9-digit numbers not already starting with "0" -> "0" + digits.

  Why: GetOrCreateCustomerByPhoneAsync (and every other phone lookup in
  CustomerStorage) used the raw digit-strip normalizer, so the same real phone
  in local (05x...) vs international (+972 5x...) format created TWO Customer
  rows for the same person - each with its own order history, so a returning
  customer could show as "new" and their saved payment methods could split
  across two records. See Preview_Customer_CanonicalizePhone.sql for the exact
  scope this backfill acts on (run it first - it is read-only).

  As of 2026-08-24 the preview found: 9,196 customers in scope, 34 simple
  rewrites (no collision), 2 duplicate groups / 4 rows requiring a merge
  (both at SiteId 35). Re-run the preview before executing this, in case the
  numbers have moved.

  Merge rule for duplicate groups: the row with more live (non-deleted) orders
  wins; ties break to the earlier CreationTime. The losing row's orders and
  saved payment methods are reassigned to the winner, then the loser is
  soft-deleted (its NormalizedPhone is blanked first so the winner can take
  the canonical value - the unique index on (SiteId, NormalizedPhone) is NOT
  filtered by IsDeleted, so a soft-deleted row still occupies its slot).

  Verified via codebase search (2026-08-24): the only FK references to
  Customer.Id are Order.CustomerId and CustomerPaymentMethod.CustomerId - if
  a new FK to Customer has been added since, re-verify before running this.

  Run on a DB backup first. Review the preview + printed counts, then
  uncomment and run the block below; it ends with COMMIT/ROLLBACK left for
  you to choose explicitly.
*/

SET NOCOUNT ON;

-- ---------------------------------------------------------------------------
-- Preview (read-only) - same scope as Preview_Customer_CanonicalizePhone.sql,
-- condensed to the counts you should compare against the header above.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Canon') IS NOT NULL DROP TABLE #Canon;

SELECT
    c.Id, c.SiteId, c.NormalizedPhone, c.IsDeleted,
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

SELECT
    (SELECT COUNT(*) FROM #Canon)                                          AS CustomersInScope,
    (SELECT COUNT(*) FROM #Canon WHERE CanonicalPhone <> NormalizedPhone)  AS RowsNeedingRewrite,
    (SELECT COUNT(*) FROM (
        SELECT SiteId, CanonicalPhone FROM #Canon
        GROUP BY SiteId, CanonicalPhone HAVING COUNT(*) > 1
    ) g)                                                                    AS DuplicateGroups;

GO

-- ---------------------------------------------------------------------------
-- BACKFILL (uncomment after reviewing the preview above and the printed counts)
-- ---------------------------------------------------------------------------
/*
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Own temp tables, distinct from the read-only preview section above (#Canon) -
-- this block must not depend on state left behind by a prior partial run.
IF OBJECT_ID('tempdb..#CanonBF') IS NOT NULL DROP TABLE #CanonBF;
IF OBJECT_ID('tempdb..#MergeRank') IS NOT NULL DROP TABLE #MergeRank;

SELECT
    c.Id, c.SiteId, c.NormalizedPhone, c.IsDeleted, c.CreationTime,
    CASE
        WHEN LEN(c.NormalizedPhone) IN (11,12) AND LEFT(c.NormalizedPhone,3) = '972'
            THEN '0' + SUBSTRING(c.NormalizedPhone,4,20)
        WHEN LEN(c.NormalizedPhone) IN (8,9) AND LEFT(c.NormalizedPhone,1) <> '0'
            THEN '0' + c.NormalizedPhone
        ELSE c.NormalizedPhone
    END AS CanonicalPhone
INTO #CanonBF
FROM dbo.Customer c
WHERE c.IsDeleted = 0 AND c.NormalizedPhone <> '';

-- Rank rows within each duplicate (SiteId, CanonicalPhone) group: winner = rn 1
-- (more live orders wins; ties -> earlier CreationTime).
SELECT x.Id, x.SiteId, x.CanonicalPhone, x.CreationTime,
       ROW_NUMBER() OVER (
           PARTITION BY x.SiteId, x.CanonicalPhone
           ORDER BY (SELECT COUNT(*) FROM dbo.[Order] o WHERE o.CustomerId = x.Id AND o.IsDeleted = 0) DESC,
                    x.CreationTime ASC
       ) AS rn
INTO #MergeRank
FROM #CanonBF x
WHERE EXISTS (
    SELECT 1 FROM #CanonBF y
    WHERE y.SiteId = x.SiteId AND y.CanonicalPhone = x.CanonicalPhone AND y.Id <> x.Id
);

DECLARE @Rows int;

-- 1. Reassign losing rows' orders to the group winner.
UPDATE o
SET o.CustomerId = w.Id
FROM dbo.[Order] o
JOIN #MergeRank l ON l.Id = o.CustomerId AND l.rn > 1
JOIN #MergeRank w ON w.SiteId = l.SiteId AND w.CanonicalPhone = l.CanonicalPhone AND w.rn = 1;
SET @Rows = @@ROWCOUNT;
PRINT CONCAT('Order rows reassigned to merge winner: ', @Rows);

-- 2. Reassign losing rows' saved payment methods to the group winner.
UPDATE pm
SET pm.CustomerId = w.Id
FROM dbo.CustomerPaymentMethod pm
JOIN #MergeRank l ON l.Id = pm.CustomerId AND l.rn > 1
JOIN #MergeRank w ON w.SiteId = l.SiteId AND w.CanonicalPhone = l.CanonicalPhone AND w.rn = 1;
SET @Rows = @@ROWCOUNT;
PRINT CONCAT('CustomerPaymentMethod rows reassigned to merge winner: ', @Rows);

-- 3. Vacate losers' NormalizedPhone (unique index isn't IsDeleted-filtered) and soft-delete them.
UPDATE c
SET c.NormalizedPhone = '', c.IsDeleted = 1, c.UpdatedDate = GETUTCDATE()
FROM dbo.Customer c
JOIN #MergeRank l ON l.Id = c.Id AND l.rn > 1;
SET @Rows = @@ROWCOUNT;
PRINT CONCAT('Losing Customer rows soft-deleted: ', @Rows);

-- 4. Now safe to write the canonical phone onto the winner.
UPDATE c
SET c.NormalizedPhone = w.CanonicalPhone, c.UpdatedDate = GETUTCDATE()
FROM dbo.Customer c
JOIN #MergeRank w ON w.Id = c.Id AND w.rn = 1;
SET @Rows = @@ROWCOUNT;
PRINT CONCAT('Merge-winner Customer rows updated to canonical phone: ', @Rows);

-- 5. Simple rewrites: rows not involved in any merge, just format-normalize in place.
UPDATE c
SET c.NormalizedPhone = x.CanonicalPhone, c.UpdatedDate = GETUTCDATE()
FROM dbo.Customer c
JOIN #CanonBF x ON x.Id = c.Id
WHERE x.CanonicalPhone <> x.NormalizedPhone
  AND NOT EXISTS (
      SELECT 1 FROM #CanonBF y
      WHERE y.SiteId = x.SiteId AND y.CanonicalPhone = x.CanonicalPhone AND y.Id <> x.Id
  );
SET @Rows = @@ROWCOUNT;
PRINT CONCAT('Standalone Customer rows re-normalized: ', @Rows);

-- Verify: should both be 0 now.
SELECT
    (SELECT COUNT(*) FROM dbo.Customer WHERE IsDeleted = 0 AND NormalizedPhone <> ''
        AND (
            (LEN(NormalizedPhone) IN (11,12) AND LEFT(NormalizedPhone,3) = '972')
            OR (LEN(NormalizedPhone) IN (8,9) AND LEFT(NormalizedPhone,1) <> '0')
        )) AS StillNonCanonical,
    (SELECT COUNT(*) FROM (
        SELECT SiteId, NormalizedPhone FROM dbo.Customer
        WHERE IsDeleted = 0 AND NormalizedPhone <> ''
        GROUP BY SiteId, NormalizedPhone HAVING COUNT(*) > 1
    ) g) AS StillDuplicateGroups;

COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
*/
