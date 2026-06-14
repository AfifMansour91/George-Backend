-- Promotions: retire IsDraft — visibility is IsActive + schedule only.
-- Run once after deploying the IsDraft removal change.

UPDATE dbo.Promotion
SET IsDraft = 0
WHERE IsDraft = 1;
