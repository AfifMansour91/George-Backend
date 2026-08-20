-- Categories imported from WooCommerce were created without IsEnabled (NULL).
-- Order building (NewManualOrderPage / picking add-item) and every report filter categories with
-- IsEnabled = 1, so all imported, never-edited categories were invisible there (JOSEPH: none of the
-- categories showed in "בניית הזמנה").
--
-- Code fixes deployed alongside this script:
--   * WooCommerceService import now sets IsEnabled = true on create and backfills NULL on update.
--   * CategoryStorage now treats NULL as enabled ((IsEnabled ?? true)), so this backfill is belt-and-braces
--     for reporting/consistency rather than strictly required.

------------------------------------------------------------------------------------------------
-- 0. Preview: how many categories are hidden per account
------------------------------------------------------------------------------------------------
SELECT c.AccountId, COUNT(*) AS HiddenCategories
FROM Category c
WHERE c.IsEnabled IS NULL AND c.IsDeleted = 0
GROUP BY c.AccountId
ORDER BY c.AccountId;

------------------------------------------------------------------------------------------------
-- 1. THE FIX (check the count, then COMMIT)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

UPDATE Category SET IsEnabled = 1
WHERE IsEnabled IS NULL;
SELECT @@ROWCOUNT AS CategoriesEnabled;

-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 2. Verify: should return ZERO rows after COMMIT
------------------------------------------------------------------------------------------------
SELECT Id, AccountId, Name FROM Category WHERE IsEnabled IS NULL;
