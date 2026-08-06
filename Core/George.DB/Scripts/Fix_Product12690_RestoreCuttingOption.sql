-- Fix: restore the soft-deleted "צורת חיתוך" ProductOption for product 12690 so its live
-- variations (21856/21857) sync to Woo again. All 3 option generations were IsDeleted=1
-- while the live variants still reference the option (selected_site edit-drop footprint).

DECLARE @ProductId INT = 12690;
DECLARE @OptionId INT = 8496; -- newest generation

-- 1. Verify the option's values cover the live variants' values ('אצבע 1', 'מינוט') before restoring.
SELECT pov.ProductOptionId, pov.Value
FROM ProductOptionValue pov
WHERE pov.ProductOptionId = @OptionId;

-- 2. Restore the newest option row (run after confirming step 1 shows the two expected values).
UPDATE ProductOption
SET IsDeleted = 0
WHERE Id = @OptionId AND ProductId = @ProductId;

-- 3. Sanity: exactly ONE non-deleted option must remain for the product.
SELECT Id, Name, IsDeleted
FROM ProductOption
WHERE ProductId = @ProductId;
